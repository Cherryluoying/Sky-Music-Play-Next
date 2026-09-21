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
