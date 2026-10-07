namespace DTA.Shared.Enums;

public enum PlatformKind
{
    Unknown = 0,
    LDPlayer = 1,
    MEmu = 2,
    AndroidApk = 3,
    Mock = 4
}

public enum FeatureId
{
    None = 0,
    Fishing = 1,
    Mining = 2,
    Insect = 3,
    Excavation = 4,
    Farm = 5,
    Collect = 6,
    Teleport = 7,
    Esp = 8,
    Settings = 9
}

public enum TeleportStatusCode
{
    Success = 0,
    RiskNotAccepted = 1,
    InvalidCoordinates = 2,
    ErrNoValidGround = 3,
    ErrFallDetected = 4,
    ConnectFailed = 5,
    MapLoadTimeout = 6,
    PlayerNotReady = 7,
    TeleportFailed = 8,
    VerifyFailed = 9,
    Cancelled = 10
}

public enum FishingSessionState
{
    Idle = 0,
    Casting = 1,
    WaitingBite = 2,
    Biting = 3,
    Reeling = 4,
    CatchResult = 5
}

public enum MoveMode
{
    Walk = 0,
    Teleport = 1
}

public enum RewardAction
{
    Keep = 0,
    Sell = 1
}
