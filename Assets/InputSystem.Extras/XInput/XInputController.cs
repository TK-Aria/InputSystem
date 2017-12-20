using System;
using UnityEngine;

namespace ISX.XInput
{
    /// <summary>
    /// An XInput compatible game controller.
    /// </summary>
    /// <remarks>
    /// Note that on non-Microsoft platforms, XInput controllers will not actually use the XInput interface
    /// but will rather be interfaced with through different APIs -- on OSX, for example, HID is used to
    /// interface with Xbox controlllers. In those cases, XInput-specific functionality (like <see cref="Capabilities"/>)
    /// will not be available.
    /// </remarks>
    public class XInputController : Gamepad
    {
        /// <summary>
        /// What specific kind of XInput controller this is.
        /// </summary>
        /// <remarks>
        /// When the controller is picked up through interfaces other than XInput or through old versions of
        /// XInput, this will always be <see cref="DeviceSubType.Unknown"/>. Put another way, this value is
        /// meaningful only on recent Microsoft platforms.
        /// </remarks>
        public DeviceSubType subType
        {
            get
            {
                if (!m_HaveParsedCapabilities)
                    ParseCapabilities();
                return m_SubType;
            }
        }

        private bool m_HaveParsedCapabilities;
        private DeviceSubType m_SubType;

        private void ParseCapabilities()
        {
            if (!string.IsNullOrEmpty(description.capabilities))
            {
                var capabilities = JsonUtility.FromJson<Capabilities>(description.capabilities);
                m_SubType = capabilities.subType;
            }
            m_HaveParsedCapabilities = true;
        }

        public enum DeviceType
        {
            Gamepad = 0x00
        }

        public enum DeviceSubType
        {
            Unknown = 0x00,
            Gamepad = 0x01,
            Wheel = 0x02,
            ArcadeStick = 0x03,
            FlightStick = 0x04,
            DancePad = 0x05,
            Guitar = 0x06,
            GuitarAlternate = 0x07,
            DrumKit = 0x08,
            GuitarBass = 0x0B,
            ArcadePad = 0x13
        }

        [Flags]
        public enum Flags
        {
            ForceFeedbackSupported = 0x01,
            Wireless = 0x02,
            VoiceSupported = 0x04,
            PluginModulesSupported = 0x08,
            NoNavigation = 0x10,
        }

        [Serializable]
        public struct Capabilities
        {
            public DeviceType type;
            public DeviceSubType subType;
            public Flags flags;
        }
    }
}
