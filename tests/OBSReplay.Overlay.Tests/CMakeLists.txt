cmake_minimum_required(VERSION 3.20)
project(OBSReplayOverlayTests LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(FetchContent)
FetchContent_Declare(
    googletest
    GIT_REPOSITORY https://github.com/google/googletest.git
    GIT_TAG v1.14.0
)
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(googletest)

enable_testing()

set(OVERLAY_SRC_DIR ${CMAKE_CURRENT_SOURCE_DIR}/../../src/OBSReplay.Overlay)

add_executable(OverlayTests
    IpcClientTests.cpp
    OverlayStateTests.cpp
    ThemeTests.cpp
    ${OVERLAY_SRC_DIR}/IpcClient.cpp
)

target_include_directories(OverlayTests PRIVATE
    ${OVERLAY_SRC_DIR}
    ${OVERLAY_SRC_DIR}/vendor
    ${OVERLAY_SRC_DIR}/vendor/imgui
)

target_link_libraries(OverlayTests PRIVATE GTest::gtest_main)
include(GoogleTest)
gtest_discover_tests(OverlayTests)
