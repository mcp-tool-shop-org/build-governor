cmake_minimum_required(VERSION 3.20)
project(ParallelTest CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(parallel_test
    main.cpp
    module_a.cpp
    module_b.cpp
    module_c.cpp
    module_d.cpp
)
