# 模块：SkyMusic.VstHost VST3 隔离宿主
cmake_minimum_required(VERSION 3.25)
project(SkyMusicVstHost LANGUAGES CXX)

include(CTest)

if(NOT WIN32)
    message(FATAL_ERROR "SkyMusic.VstHost currently supports Windows only")
endif()

set(SKYMUSIC_VST3_SDK_ROOT "" CACHE PATH "Path to the Steinberg VST3 SDK root")
if(NOT EXISTS "${SKYMUSIC_VST3_SDK_ROOT}/CMakeLists.txt")
    message(FATAL_ERROR "Set SKYMUSIC_VST3_SDK_ROOT to the vst3sdk directory")
endif()

set(SMTG_ENABLE_VST3_PLUGIN_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SMTG_ENABLE_VST3_HOSTING_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SMTG_ENABLE_VSTGUI_SUPPORT OFF CACHE BOOL "" FORCE)
set(SMTG_CREATE_PLUGIN_LINK OFF CACHE BOOL "" FORCE)
add_subdirectory("${SKYMUSIC_VST3_SDK_ROOT}" "${CMAKE_CURRENT_BINARY_DIR}/vst3sdk")
add_subdirectory("../SkyMusic.AudioEngine" "${CMAKE_CURRENT_BINARY_DIR}/audio-engine")

add_executable(SkyMusic.VstHost
    src/main.cpp
    src/host.cpp
    src/host.h
    src/sequence.cpp
    src/sequence.h
    src/protocol.cpp
    src/protocol.h
    src/spsc_queue.h
    "${SKYMUSIC_VST3_SDK_ROOT}/public.sdk/source/vst/hosting/plugprovider.cpp"
    "${SKYMUSIC_VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module_win32.cpp"
)

target_compile_features(SkyMusic.VstHost PRIVATE cxx_std_20)
target_compile_options(SkyMusic.VstHost PRIVATE /EHsc /utf-8 /Zc:char8_t-)
target_compile_definitions(SkyMusic.VstHost PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
target_include_directories(SkyMusic.VstHost PRIVATE
    "${SKYMUSIC_VST3_SDK_ROOT}/vstgui4/vstgui/thirdparty/rapidjson/include"
)
target_link_libraries(SkyMusic.VstHost PRIVATE sdk_hosting SkyMusic.AudioEngine)
set_target_properties(SkyMusic.VstHost PROPERTIES LINK_FLAGS "/SUBSYSTEM:CONSOLE")

if(BUILD_TESTING)
    add_executable(SkyMusic.VstHost.Sequence.Tests tests/sequence_tests.cpp src/sequence.cpp src/protocol.cpp)
    target_compile_features(SkyMusic.VstHost.Sequence.Tests PRIVATE cxx_std_20)
    target_compile_options(SkyMusic.VstHost.Sequence.Tests PRIVATE /EHsc /utf-8)
    target_include_directories(SkyMusic.VstHost.Sequence.Tests PRIVATE src
        "${SKYMUSIC_VST3_SDK_ROOT}/vstgui4/vstgui/thirdparty/rapidjson/include")
    add_test(NAME SkyMusic.VstHost.Sequence COMMAND SkyMusic.VstHost.Sequence.Tests)
    # 手动诊断：需指定本机插件和事件 JSON，不加入依赖第三方插件的自动测试。
    add_executable(SkyMusic.VstHost.RenderProbe tests/render_probe.cpp src/host.cpp src/sequence.cpp src/protocol.cpp
        "${SKYMUSIC_VST3_SDK_ROOT}/public.sdk/source/vst/hosting/plugprovider.cpp"
        "${SKYMUSIC_VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module_win32.cpp")
    target_compile_features(SkyMusic.VstHost.RenderProbe PRIVATE cxx_std_20)
    target_compile_options(SkyMusic.VstHost.RenderProbe PRIVATE /EHsc /utf-8 /Zc:char8_t-)
    target_compile_definitions(SkyMusic.VstHost.RenderProbe PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
    target_include_directories(SkyMusic.VstHost.RenderProbe PRIVATE src
        "${SKYMUSIC_VST3_SDK_ROOT}/vstgui4/vstgui/thirdparty/rapidjson/include")
    target_link_libraries(SkyMusic.VstHost.RenderProbe PRIVATE sdk_hosting SkyMusic.AudioEngine)
endif()
