# Modular Cave Generation System

## Overview

This package provides a modular, extensible cave generation system for Unity. It uses the cellular automata algorithm to generate organic-looking cave structures with customisable parameters.

## Features

- Highly customisable cave generation parameters
- Optimised mesh generation for improved performance
- Support for generating rooms, chambers, and connecting corridors
- Start and end point placement
- Save/load capability for generation parameters
- Custom editor for improved workflow

## How to Use

1. Create a new GameObject in your scene
2. Add the `CaveGenerator` component to it
3. Configure the generation parameters
4. Add the necessary prefabs for walls, floors, and markers
5. Click "Generate Cave" or enter Play mode to generate the cave

## Components

### CaveGenerator

The main controller component that coordinates the cave generation process.

### CaveParameters

Holds all the parameters for cave generation, including:
- Basic dimensions (width, height)
- Fill probability
- Smoothing parameters
- Room and chamber generation
- Visualisation settings

### CaveGrid

Represents the grid data structure of the cave.

### CaveAlgorithms

Contains all the algorithms used for cave generation and processing.

### CaveVisualiser

Handles the 3D visualisation of the cave.

### CaveGenerationPreset

ScriptableObject for storing and reusing cave generation parameters.

## Parameters Explained

### Basic Settings

- **Width/Height**: Dimensions of the cave in cells
- **Fill Probability**: The initial probability of a cell being a wall (higher = more walls)
- **Smooth Iterations**: Number of cellular automata iterations to apply
- **Min Room Size**: Minimum size of a room to be considered valid
- **Min Floor Percentage**: Minimum percentage of the cave that should be floor tiles

### Smoothing Parameters

- **Birth Limit**: Number of neighbors needed for a wall to be created
- **Death Limit**: Number of neighbors needed for a wall to survive
- **Use Weighted Smoothing**: Use different weights for cardinal and diagonal neighbors
- **Cardinal/Diagonal Weight**: Weights for different neighbor types

### Cave Features

- **Generate Rooms**: Create rectangular rooms in the cave
- **Generate Chambers**: Create circular chambers in the cave

### Visualisation

- **Visualise 3D**: Enable 3D visualisation
- **Cell Size**: Size of each cell in world units
- **Wall Height**: Height of wall cells
- **Prefabs**: References to the prefabs to use for visualisation

## Performance Considerations

For large caves, the system uses mesh combining to reduce the number of GameObjects and draw calls. This significantly improves performance, especially for runtime generation.

## Extending the System

The modular architecture makes it easy to extend the system with additional features:

1. Add new parameters to the `CaveParameters` class
2. Add new algorithms to the `CaveAlgorithms` class
3. Implement custom visualisation in the `CaveVisualiser` class

## Example Generation Presets

- **Tight Passages**: High fill probability, low death limit
- **Open Caverns**: Low fill probability, high death limit
- **Maze-like**: Medium fill probability, high birth limit, low death limit
- **Natural Cave**: Medium fill probability, weighted smoothing

## Known Limitations

- Large caves may take a moment to generate and visualise
- The system is designed for 2D or 2.5D caves and does not support full 3D structures like overhangs
