# Skin & Decal System Setup Instructions

## Overview
The skin and decal selection system has been implemented. Follow these steps to configure it in Unity.

## Unity Editor Setup

### 1. ComponentEntry Prefab Configuration
Locate the `ComponentEntry` prefab and assign the following fields:

**ComponentEntryUI Component:**
- `Skin Button` → Assign your Skin button GameObject
- `Decal Button` → Assign your Decal button GameObject  
- `Skin Decal View` → Assign the viewport/container Transform (should be hidden by default)
- `Skin Decal Panel Prefab` → Create and assign the SkinDecalPanel prefab (see step 2)

### 2. Create SkinDecalPanel Prefab
Create a new prefab with the following hierarchy:

```
SkinDecalPanel (GameObject with SkinDecalPanel.cs)
├── PurchasePromptText (TextMeshPro Text - for "Purchase pack" message)
└── ScrollView (ScrollRect)
    └── Viewport (RectTransform with Mask)
        └── Content (RectTransform with GridLayoutGroup)
```

**SkinDecalPanel.cs Configuration:**
- `Scroll View` → Assign the ScrollRect component
- `Content Parent` → Assign the Content transform
- `Image Button Prefab` → Create a simple prefab with Button + RawImage (see step 3)
- `Purchase Prompt Text` → Assign the TextMeshPro text for empty folder message
- `Button Size` → Set to (80, 80) or desired size
- `Spacing` → Set to 10 or desired spacing

### 3. Create ImageButton Prefab
Create a simple prefab for the selectable image buttons:

```
ImageButton (GameObject)
├── Button (Button component)
└── RawImage (RawImage component)
```

- The `RawImage` should fill the button area
- Optional: Add a `TextMeshPro` text child for labels

### 4. Test with Sample Images
Add some PNG files to test:
- `Assets/Resources/KritaArt/Skins/` - Add test skin PNGs here
- `Assets/Resources/KritaArt/Decals/` - Add test decal PNGs here

**Import Settings for PNGs:**
- Texture Type: `Sprite (2D and UI)` or `Default`
- Read/Write Enabled: True (recommended for Resources.Load)

## How It Works

### Button Visibility
- **Skin Button**: Shows for Turret, Armor, and EngineFrame components in **Inventory view only**
- **Decal Button**: Shows for Turret components **only** in **Inventory view only**
- Both buttons hidden in Shop view

### Panel Behavior
1. Click Skin/Decal button → Viewport becomes visible
2. Panel instantiates inside viewport
3. Panel populates with images from respective Resources folder
4. If folders are empty → Shows "Purchase skin/decal pack" message
5. Click an image → Selection callback fires, viewport hides
6. Click outside panel → Panel closes, viewport hides

### Data Persistence
Selections are saved in `TankSlotDataJson`:
- `engineFrameSkinPath` - Path to engine frame skin
- `armorSkinPath` - Path to armor skin  
- `turretSkinPath` - Path to turret skin
- `turretDecalPath` - Path to turret decal (turret only)

## Debugging
Enable console to see debug logs:
- `"Opening skin/decal panel for [component]"`
- `"Loading textures from Resources/KritaArt/Skins"`
- `"Found X textures"`
- Selection confirmations

## Next Steps
- Implement texture application in `TankAssembly.cs` using MaterialPropertyBlocks
- Create actual skin/decal asset packs
- Add UI for purchasing/downloading packs from itch.io
