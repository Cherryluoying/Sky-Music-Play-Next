// 模块：编辑器参数/预设桥接回归，不需要第三方插件或声卡。
#include "component_handler.h"
#include "public.sdk/source/vst/vsteditcontroller.h"
#include <iostream>
#include <limits>
#include <stdexcept>
#include <thread>

using namespace Steinberg;
using namespace Steinberg::Vst;
static void check(bool condition, const char* message)
{
    if (!condition) throw std::runtime_error(message);
}

class TestController : public EditController
{
public:
    TestController()
    {
        parameters.addParameter(STR16("Effect mix"), nullptr, 0, 0, 0, 10);
        parameters.addParameter(STR16("Gain"), nullptr, 0, 0, 0, 20);
    }
};

static double read(ParameterChanges& changes, ParamID id)
{
    for (int i = 0; i < changes.getParameterCount(); ++i)
    {
        auto* queue = changes.getParameterData(i);
        if (queue->getParameterId() != id) continue;
        int32 offset {};
        double value {};
        check(queue->getPointCount() == 1 && queue->getPoint(0, offset, value) == kResultOk && offset == 0,
              "editor values must arrive at the next block boundary");
        return value;
    }
    throw std::runtime_error("parameter never reached processor input");
}

int main()
{
    try
    {
        auto handler = owned(new skymusic::ComponentHandler({20, 10, 10}));
        auto controller = owned(new TestController());
        controller->setComponentHandler(handler);
        check(controller->beginEdit(10) == kResultOk, "begin edit must be supported");
        check(controller->performEdit(10, 0.25) == kResultOk, "effect edit must be accepted");
        controller->performEdit(10, 0.75);
        check(controller->endEdit(10) == kResultOk, "end edit must be supported");
        ParameterChanges changes(2);
        handler->drain(changes);
        check(changes.getParameterCount() == 1 && read(changes, 10) == 0.75, "rotary edits must coalesce to their latest value");
        changes.clearQueue();
        handler->drain(changes);
        check(changes.getParameterCount() == 0, "consumed values must not overwrite later MIDI automation");
        controller->setParamNormalized(10, 0.4);
        controller->setParamNormalized(20, 0.6);
        check(handler->restartComponent(kParamValuesChanged) == kResultOk, "preset refresh must be supported");
        handler->refreshController(*controller);
        handler->drain(changes);
        check(read(changes, 10) == 0.4 && read(changes, 20) == 0.6, "preset must update all processor parameters");
        check(handler->performEdit(999, 0.5) == kInvalidArgument &&
              handler->performEdit(10, std::numeric_limits<double>::quiet_NaN()) == kInvalidArgument,
              "unknown ids and NaN must not enter audio processing");
        void* identity {};
        check(handler->queryInterface(IComponentHandler::iid, &identity) == kResultOk && identity,
              "plugin must be able to query the component handler");
        static_cast<IComponentHandler*>(identity)->release();
        // UI 快速拖动同时音频消费，不得丢失最后一次旋钮位置。
        std::atomic<bool> finished {false};
        std::thread producer([&] {
            for (int i = 0; i < 100000; ++i) handler->performEdit(10, (i % 100) / 100.0);
            handler->performEdit(10, 0.123);
            finished.store(true);
        });
        double latest {};
        do
        {
            changes.clearQueue();
            handler->drain(changes);
            if (changes.getParameterCount()) latest = read(changes, 10);
        } while (!finished.load());
        producer.join();
        changes.clearQueue();
        handler->drain(changes);
        if (changes.getParameterCount()) latest = read(changes, 10);
        check(latest == 0.123, "final editor value must survive concurrent consumption");
        controller->setComponentHandler(nullptr);
        std::cout << "Editor parameter and preset regression checks passed\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << e.what() << '\n'; return 1; }
}
