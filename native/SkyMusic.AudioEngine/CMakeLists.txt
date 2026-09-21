# 模块：SkyMusic.AudioEngine 实时与离线音频图
set(SKYMUSIC_RTAUDIO_ROOT "${CMAKE_CURRENT_LIST_DIR}/../../third_party/rtaudio")
set(SKYMUSIC_RUBBERBAND_ROOT "${CMAKE_CURRENT_LIST_DIR}/../../third_party/rubberband")

add_library(SkyMusic.RtAudio STATIC
    "${SKYMUSIC_RTAUDIO_ROOT}/RtAudio.cpp"
)
target_compile_features(SkyMusic.RtAudio PUBLIC cxx_std_20)
target_compile_options(SkyMusic.RtAudio PRIVATE /EHsc /utf-8)
target_compile_definitions(SkyMusic.RtAudio PRIVATE __WINDOWS_WASAPI__ NOMINMAX WIN32_LEAN_AND_MEAN)
target_include_directories(SkyMusic.RtAudio PUBLIC "${SKYMUSIC_RTAUDIO_ROOT}")
target_link_libraries(SkyMusic.RtAudio PUBLIC ksuser mfplat mfuuid wmcodecdspuuid winmm ole32)

add_library(SkyMusic.RubberBand STATIC
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/rubberband-c.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/RubberBandStretcher.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/RubberBandLiveShifter.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/AudioCurveCalculator.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/CompoundAudioCurve.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/HighFrequencyAudioCurve.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/SilentAudioCurve.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/PercussiveAudioCurve.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/R2Stretcher.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/StretcherChannelData.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/faster/StretcherProcess.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/Allocators.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/BQResampler.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/FFT.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/Log.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/Profiler.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/Resampler.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/StretchCalculator.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/sysutils.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/mathmisc.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/common/Thread.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/finer/R3Stretcher.cpp"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src/finer/R3LiveShifter.cpp"
)
target_compile_features(SkyMusic.RubberBand PUBLIC cxx_std_20)
target_compile_options(SkyMusic.RubberBand PRIVATE /EHsc /utf-8)
target_compile_definitions(SkyMusic.RubberBand
    PUBLIC RUBBERBAND_STATIC
    PRIVATE
    USE_BUILTIN_FFT
    USE_BQRESAMPLER
    NO_THREAD_CHECKS
    NO_TIMING
    NOMINMAX
    _USE_MATH_DEFINES
)
target_include_directories(SkyMusic.RubberBand PUBLIC
    "${SKYMUSIC_RUBBERBAND_ROOT}"
    "${SKYMUSIC_RUBBERBAND_ROOT}/rubberband"
    "${SKYMUSIC_RUBBERBAND_ROOT}/src"
)

add_library(SkyMusic.AudioEngine STATIC
    src/audio_clock.cpp
    src/audio_graph.cpp
    src/offline_renderer.cpp
    src/rtaudio_device.cpp
    src/rubber_band_realtime_processor.cpp
    src/sample_player_node.cpp
    src/wasapi_audio_device.cpp
)

target_compile_features(SkyMusic.AudioEngine PUBLIC cxx_std_20)
target_compile_options(SkyMusic.AudioEngine PRIVATE /EHsc /utf-8)
target_compile_definitions(SkyMusic.AudioEngine PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
target_include_directories(SkyMusic.AudioEngine PUBLIC include)
target_link_libraries(SkyMusic.AudioEngine PUBLIC SkyMusic.RtAudio SkyMusic.RubberBand ole32 uuid avrt)

if(BUILD_TESTING)
    add_executable(SkyMusic.AudioEngine.Tests tests/audio_engine_tests.cpp)
    target_compile_options(SkyMusic.AudioEngine.Tests PRIVATE /EHsc /utf-8)
    target_link_libraries(SkyMusic.AudioEngine.Tests PRIVATE SkyMusic.AudioEngine)
    add_test(NAME SkyMusic.AudioEngine.Tests COMMAND SkyMusic.AudioEngine.Tests)
endif()
