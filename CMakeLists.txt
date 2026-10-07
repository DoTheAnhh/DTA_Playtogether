cmake_minimum_required(VERSION 3.20)
project(DTA_Playtogether CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_compile_definitions(UNICODE _UNICODE)

# Strict warnings
if(MSVC)
    add_compile_options(/W4 /permissive- /EHsc /utf-8)
else()
    add_compile_options(-Wall -Wextra -pedantic)
endif()

# Global includes
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/src)

# Shared sources
file(GLOB_RECURSE SHARED_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/shared/*.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/shared/*.hpp
)

# Client sources
file(GLOB_RECURSE CLIENT_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/client/*.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/client/*.hpp
)

# Server sources
file(GLOB_RECURSE SERVER_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/server/*.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/src/server/*.hpp
)

# --- Target: DTA_Playtogether (Client) ---
add_executable(DTA_Playtogether
    ${CLIENT_SOURCES}
    ${SHARED_SOURCES}
)

if(WIN32)
    target_link_libraries(DTA_Playtogether PRIVATE ws2_32 comctl32 dwmapi)
    if(MSVC)
        target_link_options(DTA_Playtogether PRIVATE /SUBSYSTEM:WINDOWS /ENTRY:mainCRTStartup)
    endif()
endif()

set_target_properties(DTA_Playtogether PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin/Client"
)

# --- Target: DTA_Server (Server) ---
add_executable(DTA_Server
    ${SERVER_SOURCES}
    ${SHARED_SOURCES}
)

if(WIN32)
    target_link_libraries(DTA_Server PRIVATE ws2_32 comctl32 dwmapi)
    if(MSVC)
        target_link_options(DTA_Server PRIVATE /SUBSYSTEM:WINDOWS /ENTRY:mainCRTStartup)
    endif()
endif()

set_target_properties(DTA_Server PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin/Server"
)
