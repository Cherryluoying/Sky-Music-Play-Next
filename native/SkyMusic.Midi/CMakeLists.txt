# 模块：SkyMusic.Midi RtMidi 设备桥接
cmake_minimum_required(VERSION 3.24)
project(SkyMusicMidi LANGUAGES CXX)

include(CTest)

if(NOT WIN32)
    message(FATAL_ERROR "SkyMusic.Midi currently supports Windows only")
endif()

set(SKYMUSIC_RTMIDI_ROOT "${CMAKE_CURRENT_LIST_DIR}/../../third_party/RtMidi" CACHE PATH "Path to RtMidi")
if(NOT EXISTS "${SKYMUSIC_RTMIDI_ROOT}/CMakeLists.txt")
    message(FATAL_ERROR "RtMidi source was not found")
endif()

set(RTMIDI_BUILD_STATIC_LIBS ON CACHE BOOL "" FORCE)
set(RTMIDI_BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(RTMIDI_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(RTMIDI_API_WINMM ON CACHE BOOL "" FORCE)
add_subdirectory("${SKYMUSIC_RTMIDI_ROOT}" "${CMAKE_CURRENT_BINARY_DIR}/rtmidi")
target_compile_options(rtmidi PRIVATE /EHsc /utf-8)

add_library(SkyMusic.Midi SHARED
    src/midi_api.cpp
    include/skymusic/midi/midi_api.h
)
target_compile_features(SkyMusic.Midi PRIVATE cxx_std_20)
target_compile_options(SkyMusic.Midi PRIVATE /EHsc /utf-8)
target_compile_definitions(SkyMusic.Midi PRIVATE SKYMUSIC_MIDI_EXPORTS NOMINMAX WIN32_LEAN_AND_MEAN)
target_include_directories(SkyMusic.Midi PUBLIC include)
target_link_libraries(SkyMusic.Midi PRIVATE rtmidi)

if(BUILD_TESTING)
    add_executable(SkyMusic.Midi.Tests tests/midi_api_tests.cpp)
    target_compile_features(SkyMusic.Midi.Tests PRIVATE cxx_std_20)
    target_compile_options(SkyMusic.Midi.Tests PRIVATE /EHsc /utf-8)
    target_link_libraries(SkyMusic.Midi.Tests PRIVATE SkyMusic.Midi)
    add_test(NAME SkyMusic.Midi.Tests COMMAND SkyMusic.Midi.Tests)
endif()
