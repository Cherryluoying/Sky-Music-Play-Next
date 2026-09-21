// 模块：SkyMusic.VstHost 原生实现 spsc_queue
#pragma once

#include <array>
#include <atomic>
#include <cstddef>

namespace skymusic
{
template <typename T, std::size_t Capacity>
class SpscQueue
{
public:
    bool push(const T& value) noexcept
    {
        const auto head = head_.load(std::memory_order_relaxed);
        const auto next = increment(head);
        if (next == tail_.load(std::memory_order_acquire))
            return false;
        values_[head] = value;
        head_.store(next, std::memory_order_release);
        return true;
    }

    bool pop(T& value) noexcept
    {
        const auto tail = tail_.load(std::memory_order_relaxed);
        if (tail == head_.load(std::memory_order_acquire))
            return false;
        value = values_[tail];
        tail_.store(increment(tail), std::memory_order_release);
        return true;
    }

private:
    static constexpr std::size_t increment(std::size_t value) noexcept
    {
        return (value + 1) % Capacity;
    }

    std::array<T, Capacity> values_ {};
    std::atomic<std::size_t> head_ {0};
    std::atomic<std::size_t> tail_ {0};
};
}
