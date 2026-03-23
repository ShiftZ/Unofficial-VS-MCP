cmake_minimum_required(VERSION 3.20)
project(CTestSample LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)

enable_testing()

add_executable(ctest_runner test.cpp)

add_test(NAME Addition COMMAND ctest_runner addition)
add_test(NAME Subtraction COMMAND ctest_runner subtraction)
add_test(NAME StringCompare COMMAND ctest_runner string_compare)
