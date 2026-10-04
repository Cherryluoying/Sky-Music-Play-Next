// 模块：插件编辑器参数桥接。每个参数只保留最新值，音频线程不加锁、不调用控制器。
#pragma once
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include <algorithm>
#include <atomic>
#include <cmath>
#include <memory>
#include <vector>

namespace skymusic
{
class ComponentHandler final : public Steinberg::Vst::IComponentHandler
{
public:
    // 参数表在挂接控制器、启动音频线程之前建立，运行中不改变存储地址。
    explicit ComponentHandler(std::vector<Steinberg::Vst::ParamID> ids) : ids_(std::move(ids))
    {
        std::sort(ids_.begin(), ids_.end());
        ids_.erase(std::unique(ids_.begin(), ids_.end()), ids_.end());
        pending_ = std::make_unique<std::atomic<double>[]>(ids_.size());
        for (std::size_t i = 0; i < ids_.size(); ++i) pending_[i].store(-1.0);
    }

    const auto& parameterIds() const noexcept { return ids_; }
    Steinberg::tresult PLUGIN_API beginEdit(Steinberg::Vst::ParamID id) override { return contains(id); }
    Steinberg::tresult PLUGIN_API endEdit(Steinberg::Vst::ParamID id) override { return contains(id); }
    Steinberg::tresult PLUGIN_API performEdit(Steinberg::Vst::ParamID id, double value) override
    {
        const auto it = std::lower_bound(ids_.begin(), ids_.end(), id);
        if (it == ids_.end() || *it != id || !std::isfinite(value) || value < 0 || value > 1)
            return Steinberg::kInvalidArgument;
        pending_[it - ids_.begin()].store(value, std::memory_order_release);
        return Steinberg::kResultOk;
    }

    Steinberg::tresult PLUGIN_API restartComponent(Steinberg::int32 flags) override
    {
        if (flags & Steinberg::Vst::kParamValuesChanged) refresh_.store(true, std::memory_order_release);
        // 当前没有自动化轨道或宿主参数面板；名称变化不需要重启音频。
        constexpr auto supported = Steinberg::Vst::kParamValuesChanged | Steinberg::Vst::kParamTitlesChanged;
        return (flags & ~supported) ? Steinberg::kNotImplemented : Steinberg::kResultOk;
    }

    // 预设变更在主线程读取控制器，再由下一个音频块应用。
    void refreshController(Steinberg::Vst::IEditController& controller)
    {
        if (!refresh_.exchange(false, std::memory_order_acq_rel)) return;
        for (const auto id : ids_) performEdit(id, controller.getParamNormalized(id));
    }

    void drain(Steinberg::Vst::ParameterChanges& changes) noexcept
    {
        for (std::size_t i = 0; i < ids_.size(); ++i)
        {
            const auto value = pending_[i].exchange(-1.0, std::memory_order_acq_rel);
            if (value < 0) continue;
            Steinberg::int32 index {};
            changes.addParameterData(ids_[i], index)->addPoint(0, value, index);
        }
    }

    Steinberg::tresult PLUGIN_API queryInterface(const Steinberg::TUID iid, void** object) override
    {
        if (!object) return Steinberg::kInvalidArgument;
        *object = nullptr;
        if (!Steinberg::FUnknownPrivate::iidEqual(iid, IComponentHandler::iid) &&
            !Steinberg::FUnknownPrivate::iidEqual(iid, Steinberg::FUnknown::iid)) return Steinberg::kNoInterface;
        *object = static_cast<IComponentHandler*>(this);
        addRef();
        return Steinberg::kResultOk;
    }
    Steinberg::uint32 PLUGIN_API addRef() override { return ++references_; }
    Steinberg::uint32 PLUGIN_API release() override
    {
        const auto count = --references_;
        if (count == 0) delete this;
        return count;
    }

private:
    Steinberg::tresult contains(Steinberg::Vst::ParamID id) const
    {
        return std::binary_search(ids_.begin(), ids_.end(), id) ? Steinberg::kResultOk : Steinberg::kInvalidArgument;
    }
    static_assert(std::atomic<double>::is_always_lock_free);
    std::atomic<Steinberg::uint32> references_ {1};
    std::vector<Steinberg::Vst::ParamID> ids_;
    std::unique_ptr<std::atomic<double>[]> pending_;
    std::atomic<bool> refresh_ {false};
};
}
