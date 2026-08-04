using System;
using UnityEngine;

namespace Lotusim
{
    [AttributeUsage(AttributeTargets.Field)]
    public class KeyDescriptionAttribute : Attribute
    {
        public string Category { get; }
        public string Description { get; }

        public KeyDescriptionAttribute(string category, string description)
        {
            Category = category;
            Description = description;
        }
    }

    public static class KeyBindings
    {
        // Camera Movement
        [KeyDescription("Camera Movement", "Move Forward")]
        public static KeyCode Forward = KeyCode.W;

        [KeyDescription("Camera Movement", "Move Backward")]
        public static KeyCode Backward = KeyCode.S;

        [KeyDescription("Camera Movement", "Move Left")]
        public static KeyCode Left = KeyCode.A;

        [KeyDescription("Camera Movement", "Move Right")]
        public static KeyCode Right = KeyCode.D;

        [KeyDescription("Camera Movement", "Move Up")]
        public static KeyCode Up = KeyCode.E;

        [KeyDescription("Camera Movement", "Move Down")]
        public static KeyCode Down = KeyCode.Q;

        // Target Navigation
        [KeyDescription("Navigation", "Next Target")]
        public static KeyCode NextTarget = KeyCode.RightArrow;

        [KeyDescription("Navigation", "Previous Target")]
        public static KeyCode PrevTarget = KeyCode.LeftArrow;

        // Utilities
        [KeyDescription("Graphs & HUD", "Toggle Power Graph")]
        public static KeyCode TogglePowerGraph = KeyCode.G;

        [KeyDescription("Graphs & HUD", "Toggle Agent Battery HUD")]
        public static KeyCode ToggleBatteryHUD = KeyCode.B;

        [KeyDescription("Graphs & HUD", "Toggle LCOE Graph")]
        public static KeyCode ToggleLcoeGraph = KeyCode.L;

        [KeyDescription("Graphs & HUD", "Toggle Help Menu")]
        public static KeyCode ToggleHelp = KeyCode.H;

        [KeyDescription("Graphs & HUD", "Toggle Image Display")]
        public static KeyCode ToggleImage = KeyCode.I;

        [KeyDescription("Graphs & HUD", "Toggle Maintenance HUD")]
        public static KeyCode ToggleMaintenance = KeyCode.O;

        [KeyDescription("Graphs & HUD", "Toggle Warning HUD")]
        public static KeyCode ToggleWarning = KeyCode.K;

        [KeyDescription("Graphs & HUD", "Toggle Detection Warning")]
        public static KeyCode ToggleDetectionWarning = KeyCode.J;

        [KeyDescription("Graphs & HUD", "Prev Drone in Fleet")]
        public static KeyCode DronePrev = KeyCode.DownArrow;

        [KeyDescription("Graphs & HUD", "Next Drone in Fleet")]
        public static KeyCode DroneNext = KeyCode.UpArrow;

        // Wind Sliders
        [KeyDescription("Wind Sliders", "X-Axis Decrease")]
        public static KeyCode WindXDec = KeyCode.Alpha1;

        [KeyDescription("Wind Sliders", "X-Axis Increase")]
        public static KeyCode WindXInc = KeyCode.Alpha2;

        [KeyDescription("Wind Sliders", "Y-Axis Decrease")]
        public static KeyCode WindYDec = KeyCode.Alpha4;

        [KeyDescription("Wind Sliders", "Y-Axis Increase")]
        public static KeyCode WindYInc = KeyCode.Alpha5;

        [KeyDescription("Wind Sliders", "Z-Axis Decrease")]
        public static KeyCode WindZDec = KeyCode.Alpha7;

        [KeyDescription("Wind Sliders", "Z-Axis Increase")]
        public static KeyCode WindZInc = KeyCode.Alpha8;

        [KeyDescription("Wind Sliders", "Reset All Axes")]
        public static KeyCode WindReset = KeyCode.Alpha0;

        // Visualization
        [KeyDescription("Visualization", "Toggle Wind Field")]
        public static KeyCode ToggleWindField = KeyCode.Alpha9;

        [KeyDescription("Visualization", "Toggle Wind Regions")]
        public static KeyCode ToggleWindRegions = KeyCode.Alpha3;

        // Display Switching
        [KeyDescription("Display Switching", "Main Camera")]
        public static KeyCode DisplayMain = KeyCode.Keypad1;

        [KeyDescription("Display Switching", "Leap Camera")]
        public static KeyCode DisplayLeap = KeyCode.Keypad4;

        [KeyDescription("Display Switching", "BlueROV Camera")]
        public static KeyCode DisplayBlueROV = KeyCode.Keypad5;

        [KeyDescription("Display Switching", "WAMV Camera")]
        public static KeyCode DisplayWAMV = KeyCode.Keypad6;

        [KeyDescription("Display Switching", "X500 Camera")]
        public static KeyCode DisplayX500 = KeyCode.Keypad7;

        [KeyDescription("Display Switching", "VR Camera")]
        public static KeyCode DisplayVR = KeyCode.Keypad8;
    }
}
