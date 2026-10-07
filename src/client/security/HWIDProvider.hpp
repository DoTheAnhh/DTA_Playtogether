#pragma once
#include <string>

namespace dta::security {

class HWIDProvider {
public:
    static std::string GetHWID();
};

} // namespace dta::security
