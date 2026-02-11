cmake_minimum_required(VERSION 3.20)
project(OverlayRenderer LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# --- Fetch RmlUi + FreeType ---
include(FetchContent)

FetchContent_Declare(
    freetype
    GIT_REPOSITORY https://github.com/freetype/freetype.git
    GIT_TAG VER-2-13-3
)
set(FT_DISABLE_BZIP2 ON CACHE BOOL "" FORCE)
set(FT_DISABLE_HARFBUZZ ON CACHE BOOL "" FORCE)
set(FT_DISABLE_PNG ON CACHE BOOL "" FORCE)
set(FT_DISABLE_BROTLI ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(freetype)

# RmlUi expects Freetype::Freetype from find_package(Freetype).
# FetchContent creates 'freetype' target; create the alias RmlUi needs.
if(NOT TARGET Freetype::Freetype)
    add_library(Freetype::Freetype ALIAS freetype)
endif()

# Set variables that find_package(Freetype) would set, so RmlUi's find succeeds
set(FREETYPE_FOUND TRUE CACHE BOOL "" FORCE)
set(Freetype_FOUND TRUE CACHE BOOL "" FORCE)

FetchContent_Declare(
    rmlui
    GIT_REPOSITORY https://github.com/mikke89/RmlUi.git
    GIT_TAG 6.0
)
set(RMLUI_FONT_ENGINE "freetype" CACHE STRING "" FORCE)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(RMLUI_SAMPLES OFF CACHE BOOL "" FORCE)
set(RMLUI_TESTS OFF CACHE BOOL "" FORCE)
set(RMLUI_SVG_PLUGIN OFF CACHE BOOL "" FORCE)
set(RMLUI_LOTTIE_PLUGIN OFF CACHE BOOL "" FORCE)
set(RMLUI_LUA_BINDINGS OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(rmlui)

# --- Sources ---
set(SOURCES
    main.cpp
    OverlayApp.cpp
    OverlayDataModel.cpp
    DxRenderer.cpp
    WindowManager.cpp
    IpcClient.cpp
    PreviewRenderer.cpp
    RmlRenderInterface_DX11.cpp
    RmlSystemInterface_Win32.cpp
)

# --- Executable ---
add_executable(${PROJECT_NAME} WIN32 ${SOURCES})

target_include_directories(${PROJECT_NAME} PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/vendor
)

target_link_libraries(${PROJECT_NAME} PRIVATE
    d3d11
    dxgi
    d3dcompiler
    rmlui
)

# Copy to host output directory after build
set_target_properties(${PROJECT_NAME} PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${CMAKE_BINARY_DIR}/bin/Debug"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/bin/Release"
)

# --- Tests (optional) ---
option(BUILD_TESTS "Build unit tests" OFF)
if(BUILD_TESTS)
    FetchContent_Declare(
        googletest
        GIT_REPOSITORY https://github.com/google/googletest.git
        GIT_TAG v1.14.0
    )
    set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
    FetchContent_MakeAvailable(googletest)

    enable_testing()

    add_executable(OverlayTests
        ${CMAKE_CURRENT_SOURCE_DIR}/../../tests/ReplayOverlay.Overlay.Tests/IpcClientTests.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/../../tests/ReplayOverlay.Overlay.Tests/OverlayStateTests.cpp
        ${CMAKE_CURRENT_SOURCE_DIR}/../../tests/ReplayOverlay.Overlay.Tests/ThemeTests.cpp
        IpcClient.cpp
    )

    target_include_directories(OverlayTests PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/vendor
    )

    target_link_libraries(OverlayTests PRIVATE GTest::gtest_main)
    include(GoogleTest)
    gtest_discover_tests(OverlayTests)
endif()
