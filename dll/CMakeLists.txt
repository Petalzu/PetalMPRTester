cmake_minimum_required(VERSION 3.10)
project(MousePollingEngine LANGUAGES CXX)

set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
set(CMAKE_CXX_STANDARD 14)

if (MSVC)
    add_compile_options("/source-charset:utf-8")
    # 静态链接C/C++运行库，解除对VC++ Redistributable的依赖
    foreach(flag_var
        CMAKE_C_FLAGS_RELEASE CMAKE_C_FLAGS_DEBUG
        CMAKE_CXX_FLAGS_RELEASE CMAKE_CXX_FLAGS_DEBUG)
        string(REPLACE "/MD" "/MT" ${flag_var} "${${flag_var}}")
    endforeach()
endif()

add_library(MousePollingEngine SHARED
    MousePollingEngine.cpp
    # 你可以添加其它cpp/h文件
)

target_include_directories(MousePollingEngine PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})
target_link_libraries(MousePollingEngine winmm)