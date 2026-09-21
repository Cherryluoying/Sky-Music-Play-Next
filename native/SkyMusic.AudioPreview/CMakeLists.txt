# 模块：SkyMusic.AudioPreview 乐器采样试听
cmake_minimum_required(VERSION 3.25)
project(SkyMusicAudioPreview LANGUAGES CXX)

include(CTest)

if(NOT WIN32)
    message(FATAL_ERROR "SkyMusic.AudioPreview currently supports Windows only")
endif()

add_subdirectory("../SkyMusic.AudioEngine" "${CMAKE_CURRENT_BINARY_DIR}/audio-engine")

add_library(SkyMusic.AudioPreview SHARED
    src/media_foundation_decoder.cpp
    src/media_foundation_decoder.h
    src/preview_api.cpp
    include/skymusic/audio_preview/preview_api.h
)
target_compile_features(SkyMusic.AudioPreview PRIVATE cxx_std_20)
target_compile_options(SkyMusic.AudioPreview PRIVATE /EHsc /utf-8)
target_compile_definitions(SkyMusic.AudioPreview PRIVATE
    SKYMUSIC_AUDIO_PREVIEW_EXPORTS
    NOMINMAX
    WIN32_LEAN_AND_MEAN
)
target_include_directories(SkyMusic.AudioPreview PUBLIC include)
target_link_libraries(SkyMusic.AudioPreview PRIVATE
    SkyMusic.AudioEngine
    mfreadwrite
    mfplat
    mfuuid
    ole32
)

if(BUILD_TESTING)
    add_executable(SkyMusic.AudioPreview.Smoke tests/audio_preview_smoke.cpp)
    target_compile_features(SkyMusic.AudioPreview.Smoke PRIVATE cxx_std_20)
    target_compile_options(SkyMusic.AudioPreview.Smoke PRIVATE /EHsc /utf-8)
    target_link_libraries(SkyMusic.AudioPreview.Smoke PRIVATE SkyMusic.AudioPreview)
endif()
