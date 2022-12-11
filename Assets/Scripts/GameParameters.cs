using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// #define GAME_MODE_VR

public static class GameParameters
{
    public static bool isVRMode = false;
    public enum Language {
        None,
        Chinese,
        English,
        Japanese,
        Russian,
        French,
    }
    public static Language defaultLanguage = Language.Chinese;
    public static Language fallbackLanguage = Language.English;
}
