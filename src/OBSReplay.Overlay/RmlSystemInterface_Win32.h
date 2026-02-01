#pragma once
#include <RmlUi/Core/SystemInterface.h>

class RmlSystemInterface_Win32 : public Rml::SystemInterface
{
public:
    double GetElapsedTime() override;
    bool LogMessage(Rml::Log::Type type, const Rml::String& message) override;
    void SetClipboardText(const Rml::String& text) override;
    void GetClipboardText(Rml::String& text) override;

private:
    bool m_timerInitialized = false;
    double m_frequency = 0.0;
    long long m_startTime = 0;
};
