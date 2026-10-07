#pragma once
#include <string>

namespace dta::server::ui_schema {

class UISchemaManager {
public:
    static UISchemaManager& Instance();

    [[nodiscard]] std::string GetSchemaJson() const;
    void UpdateSchema(const std::string& newSchemaJson);

private:
    UISchemaManager();
    std::string m_cachedSchema;
};

} // namespace dta::server::ui_schema
