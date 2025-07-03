# AI Editor Number Entry System

## Overview

The AI Editor now uses a context-sensitive number entry system that only shows input fields when nodes are clicked, making the interface cleaner and more intuitive.

## How It Works

### 1. Node Types That Support Numbers

The following node types can have numeric values:

**Condition Nodes:**
- If Self HP>#
- If Self HP<#
- If HP < #
- If HP > #
- If Tag = #
- If Tag < #
- If Tag > #
- If Range<#
- If Range>#

### 2. User Interaction

1. **Click on a node** that supports numbers to show the number input field
2. The input field appears **positioned over the node**
3. **Type a number** and press Enter or click elsewhere to confirm
4. The number **replaces # or existing numbers** in the node label
5. Press **Escape** or **click elsewhere** to cancel input

### 3. Number Replacement Logic

- If the node label contains `#`, it gets replaced with the number
- If the node label already has a number, it gets replaced
- The exact format of each node type is preserved

Examples:
- `If Self HP>#` → `If Self HP>50`
- `If HP < #` → `If HP < 25`
- `If Tag = 10` → `If Tag = 5` (replaces 10 with 5)
- `If Self HP<100` → `If Self HP<75` (replaces 100 with 75)
- `If Range<#` → `If Range<30`
- `If Range>15` → `If Range>20` (replaces 15 with 20)

## Technical Implementation

### Key Components

- **AiEditorFileUI**: Main controller for the number entry system
- **InlineNumberInput**: Simplified component that stores numeric values for nodes
- **Global Input Field**: Single TMP_InputField that appears when needed

### Key Methods

- `CanNodeHaveNumber(string nodeLabel)`: Checks if a node type supports numbers
- `OnNodeClicked(GameObject nodeGO)`: Shows number input when node is clicked
- `ShowGlobalNumberInput()`: Positions and shows the input field
- `UpdateNodeLabelWithNumber()`: Updates the node's visual label with the new number

### Files Modified

- `Assets/AiEditor/AISaveFiles/AiEditorFileUI.cs`: Main implementation
- `Assets/AiEditor/Scripts/InlineNumberInput.cs`: Simplified number storage component

## Benefits

1. **Cleaner UI**: No permanent number input boxes cluttering the interface
2. **Context-Sensitive**: Input only appears when needed
3. **Intuitive**: Click-to-edit pattern familiar to users
4. **Explicit Control**: Only predefined node types can have numbers
5. **Robust**: Handles various label formats and number replacement scenarios

## Migration Notes

- Old `NumberInputButton` components are automatically hidden
- Existing AI files with numbers continue to work
- The system extracts existing numbers from node labels during loading
- Save/load functionality preserves numeric values correctly
