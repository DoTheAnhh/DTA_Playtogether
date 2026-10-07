#pragma once
#include <cstdint>

namespace dta {

enum class EmulatorType : uint8_t {
    UNKNOWN = 0,
    LDPLAYER_9_PLUS = 1,
    MEMU = 2,
    NOX = 3,
    MUMU = 4,
    ANDROID_APK_NATIVE = 5
};

enum class CommandPriority : uint8_t {
    CANCEL_STOP = 0,
    SAFETY_CHECK = 1,
    GAME_ACTION = 2,
    LOW_PRIORITY = 3
};

// Verified from ActorDefaultControl.eFishingState / dump.cs
enum class FishingState : uint32_t {
    IDLE = 0,
    CASTING = 1,
    WAITING_BITE = 3,
    SHADOW = 4,
    BITE = 5,
    REELING = 6,
    RESULT = 9,
    BIG_PUMPIN = 16,
    BIG_DRAG = 17,
    BIG_TUG = 18,
    BIG_STUN = 24,
    UNKNOWN = 99
};

// Verified from ActorDefaultControl._pickaxState / dump.cs
enum class PickaxState : uint32_t {
    NONE = 0,
    PICKAXING = 1,
    MISS = 2,
    FINISH = 3
};

// Verified from ActorDefaultControl.eExcavateState / dump.cs
enum class ExcavateState : uint32_t {
    NONE = 0,
    DIGGING = 1,
    MISS = 2,
    COMPLETE = 3,
    REWARD_REQ = 4,
    REWARD_FAIL = 5,
    BOASTING = 6,
    FINISH = 7
};

// Verified from ActorDefaultControl._collectActionState / dump.cs
enum class CollectState : uint32_t {
    NONE = 0,
    REWARD_REQ = 1,
    REWARD_WAIT = 2,
    REWARD_READY = 3,
    REWARD_SUCCESS = 4,
    REWARD_FAIL = 5,
    BOASTING = 6,
    FINISH = 7
};

// Map IDs verified from LayerSystem and TableMapImpl
enum class MapId : uint32_t {
    UNKNOWN = 0,
    PLAZA = 1,
    DOWNTOWN = 2,
    CAMPING = 3,
    RESORT = 4,
    HOME = 10,
    LOST_ISLAND = 21001
};

// Item Grades
enum class ItemGrade : uint8_t {
    COMMON = 1,   // Trắng
    UNCOMMON = 2, // Xanh lá
    RARE = 3,     // Xanh dương
    HEROIC = 4,   // Tím
    LEGENDARY = 5 // Vàng vương miện
};

// Bot Run State
enum class BotRunState : uint8_t {
    STOPPED = 0,
    STARTING = 1,
    RUNNING = 2,
    PAUSED = 3,
    ERROR_STATE = 4
};

} // namespace dta
