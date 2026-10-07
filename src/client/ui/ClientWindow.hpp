#pragma once
#include <windows.h>
#include <string>
#include <vector>
#include <memory>
#include <array>
#include "shared/Theme.hpp"

// Forward declarations
namespace dta::features::teleport { class ITeleportService; }
namespace dta::features::fishing { class FishingBot; }
namespace dta::features::mining { class MiningBot; }
namespace dta::features::insect { class InsectBot; }
namespace dta::features::excavation { class ExcavationBot; }
namespace dta::features::farm { class FarmBot; }
namespace dta::features::collect { class CollectBot; }

namespace dta::ui {

struct MasterSpotDTO {
    std::string id;
    std::string name;
    uint32_t mapId{1};
    std::string mapName{"Plaza"};
    float x{0.0f};
    float y{0.0f};
    float z{0.0f};
    bool enabled{true};
};

struct SidebarMenuItem {
    int id;
    std::wstring title;
    std::wstring icon;
};

class ClientWindow {
public:
    static ClientWindow& Instance();

    void InitDependencies(
        std::shared_ptr<features::teleport::ITeleportService> teleportService,
        std::shared_ptr<features::fishing::FishingBot> fishingBot,
        std::shared_ptr<features::mining::MiningBot> miningBot,
        std::shared_ptr<features::insect::InsectBot> insectBot,
        std::shared_ptr<features::excavation::ExcavationBot> excavationBot,
        std::shared_ptr<features::farm::FarmBot> farmBot,
        std::shared_ptr<features::collect::CollectBot> collectBot
    );

    bool Create(HINSTANCE hInstance, int nCmdShow);
    void RunMessageLoop();

    void SyncMasterSpotsFromServer();

    void SetFreeTier(bool isFree) { m_isFreeTier = isFree; }
    [[nodiscard]] bool IsFreeTier() const noexcept { return m_isFreeTier; }
    void ApplyFreeTierRestrictions();

private:
    ClientWindow();
    ~ClientWindow();

    static LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
    LRESULT HandleMessage(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

    void InitControls(HWND hWnd);
    void SwitchTab(int tabIndex);
    void RefreshTeleportSpotList();
    void ShowTabControls(int tabIndex);
    void CheckServerHealthAsync();

    // Tab control registration helper
    void RegControl(int tabIndex, HWND hCtrl) {
        if (tabIndex >= 0 && tabIndex < 9 && hCtrl) {
            m_tabControls[tabIndex].push_back(hCtrl);
        }
    }

    HINSTANCE m_hInstance{nullptr};
    HWND m_hWnd{nullptr};

    // Sidebar navigation
    std::vector<HWND> m_sidebarButtons;
    int m_activeTab{1}; // 1 = Câu cá default

    // Top Header Controls
    HWND m_hChkTopmost{nullptr};
    HWND m_hBtnSubTabFishing{nullptr};
    HWND m_hBtnSubTabHistory{nullptr};
    HWND m_hComboEmulator{nullptr};
    HWND m_hComboTab{nullptr};
    HWND m_hBtnRefreshEmu{nullptr};

    // Bottom Sidebar
    HWND m_hBtnLogoutKey{nullptr};

    // Control containers for all 9 tabs
    std::array<std::vector<HWND>, 9> m_tabControls;

    // --- TAB 0: ESP CONTROLS ---
    HWND m_hEspBtnStart{nullptr};
    HWND m_hEspBtnStop{nullptr};
    HWND m_hEspChkSnapline{nullptr};
    HWND m_hEspChk3DBox{nullptr};
    HWND m_hEspChkDistance{nullptr};
    HWND m_hEspChkTier{nullptr};
    HWND m_hEspChkFish{nullptr};
    HWND m_hEspChkOre{nullptr};
    HWND m_hEspChkInsect{nullptr};
    HWND m_hEspChkChest{nullptr};

    // --- TAB 1: CÂU CÁ (FISHING) CONTROLS ---
    HWND m_hBtnStartFishing{nullptr};
    HWND m_hBtnStopFishing{nullptr};
    HWND m_hChkAutoRepair{nullptr};
    HWND m_hChkFastSell{nullptr};
    HWND m_hBtnKeepFish{nullptr};
    HWND m_hBtnSellFish{nullptr};
    bool m_keepFishSelected{false}; // Default Sell All on Free
    HWND m_hChkLockPov{nullptr};
    HWND m_hChkFastBite{nullptr};
    HWND m_hChkFilterFish{nullptr};
    HWND m_hEditFilterId{nullptr};
    HWND m_hBtnClearFilter{nullptr};
    std::array<HWND, 7> m_btnFilterShadow{};
    std::array<bool, 7> m_filterShadowState{};
    std::array<HWND, 5> m_btnFilterTier{};
    std::array<bool, 5> m_filterTierState{};
    HWND m_hChkKeepVariant{nullptr};
    HWND m_hChkKeepMutant{nullptr};
    HWND m_hEditKeepId{nullptr};
    HWND m_hBtnClearKeep{nullptr};
    std::array<HWND, 7> m_btnKeepShadow{};
    std::array<bool, 7> m_keepShadowState{};
    std::array<HWND, 5> m_btnKeepTier{};
    std::array<bool, 5> m_keepTierState{};

    // --- TAB 2: ĐÀO CỔ VẬT CONTROLS ---
    HWND m_hExcBtnStart{nullptr};
    HWND m_hExcBtnStop{nullptr};
    HWND m_hExcChkAutoBuy{nullptr};
    HWND m_hExcChkAutoRepair{nullptr};
    HWND m_hExcChkSkipWood{nullptr};
    HWND m_hExcChkMiniGame{nullptr};
    HWND m_hExcChkOpenNow{nullptr};
    HWND m_hExcChkNextChest{nullptr};
    HWND m_hExcChkChestSilver{nullptr};
    HWND m_hExcChkChestGold{nullptr};
    HWND m_hExcChkChestDiamond{nullptr};

    // --- TAB 3: ĐẬP ĐÁ CONTROLS ---
    HWND m_hMinBtnStart{nullptr};
    HWND m_hMinBtnStop{nullptr};
    HWND m_hMinChkAutoRepair{nullptr};
    HWND m_hMinChkSwapVip{nullptr};
    HWND m_hMinChkAntiKs{nullptr};
    HWND m_hMinChkOreIron{nullptr};
    HWND m_hMinChkOreGold{nullptr};
    HWND m_hMinChkOreDiamond{nullptr};
    HWND m_hMinChkOreMeteor{nullptr};
    HWND m_hMinComboZone{nullptr};

    // --- TAB 4: BẮT BỌ CONTROLS ---
    HWND m_hInsBtnStart{nullptr};
    HWND m_hInsBtnStop{nullptr};
    HWND m_hInsChkAutoRepair{nullptr};
    HWND m_hInsChkSneak{nullptr};
    HWND m_hInsChkLockAngle{nullptr};
    HWND m_hInsChkTeleportBack{nullptr};
    HWND m_hInsChkButterfly{nullptr};
    HWND m_hInsChkBeetle{nullptr};
    HWND m_hInsChkStag{nullptr};
    HWND m_hInsChkScorpion{nullptr};

    // --- TAB 5: THU LƯỢM CONTROLS ---
    HWND m_hColBtnStart{nullptr};
    HWND m_hColBtnStop{nullptr};
    HWND m_hColChkAutoSell{nullptr};
    HWND m_hColChkAutoRecycle{nullptr};
    HWND m_hColChkOptimizeTsp{nullptr};
    HWND m_hColChkBranch{nullptr};
    HWND m_hColChkApple{nullptr};
    HWND m_hColChkMushroom{nullptr};
    HWND m_hColChkStar{nullptr};

    // --- TAB 6: NÔNG TRẠI CONTROLS ---
    HWND m_hFarmBtnStart{nullptr};
    HWND m_hFarmBtnStop{nullptr};
    HWND m_hFarmChkWater{nullptr};
    HWND m_hFarmChkFertilize{nullptr};
    HWND m_hFarmChkAutoBuySeeds{nullptr};
    HWND m_hFarmChkHarvestNow{nullptr};
    HWND m_hFarmChkChicken{nullptr};
    HWND m_hFarmChkCow{nullptr};

    // --- TAB 7: DỊCH CHUYỂN CONTROLS ---
    HWND m_hTeleSpotList{nullptr};
    HWND m_hBtnSyncSpots{nullptr};
    HWND m_hBtnTeleportInstant{nullptr};
    HWND m_hBtnSaveCurrentPos{nullptr};
    HWND m_hBtnZonePlaza{nullptr};
    HWND m_hBtnZoneDowntown{nullptr};
    HWND m_hBtnZoneCamping{nullptr};
    HWND m_hBtnZoneResort{nullptr};
    HWND m_hBtnZoneHome{nullptr};

    // --- TAB 8: CÀI ĐẶT CONTROLS ---
    HWND m_hEditKey{nullptr};
    HWND m_hBtnActivateKey{nullptr};
    HWND m_hStaticHWID{nullptr};
    HWND m_hBtnCopyHWID{nullptr};
    HWND m_hEditServerUrl{nullptr};
    HWND m_hBtnPingServer{nullptr};
    HWND m_hChkAntiBan{nullptr};
    HWND m_hChkPanicF12{nullptr};

    // Theme GDI Brushes & Fonts
    HBRUSH m_hBgBrush{nullptr};
    HBRUSH m_hSidebarBrush{nullptr};
    HBRUSH m_hCardBrush{nullptr};
    HBRUSH m_hInputBrush{nullptr};
    HBRUSH m_hBorderBrush{nullptr};

    HFONT m_hFontTitle{nullptr};
    HFONT m_hFontHeader{nullptr};
    HFONT m_hFontRegular{nullptr};
    HFONT m_hFontBold{nullptr};
    HFONT m_hFontSmall{nullptr};
    HFONT m_hFontMono{nullptr};
    HFONT m_hFontBigNum{nullptr};

    // Status Cache
    std::vector<MasterSpotDTO> m_masterSpots;
    std::string m_clientHwid;
    bool m_isBotRunning{false};
    bool m_isFreeTier{false};
    bool m_isServerOnline{false};

    // Feature dependencies
    std::shared_ptr<features::teleport::ITeleportService> m_teleportService;
    std::shared_ptr<features::fishing::FishingBot> m_fishingBot;
    std::shared_ptr<features::mining::MiningBot> m_miningBot;
    std::shared_ptr<features::insect::InsectBot> m_insectBot;
    std::shared_ptr<features::excavation::ExcavationBot> m_excavationBot;
    std::shared_ptr<features::farm::FarmBot> m_farmBot;
    std::shared_ptr<features::collect::CollectBot> m_collectBot;
};

} // namespace dta::ui
