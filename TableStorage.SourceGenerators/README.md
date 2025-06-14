# TableStorage Source Generators - Refactored Architecture

This directory contains the refactored TableStorage source generators that follow good coding practices and separation of concerns.

## Architecture Overview

The code has been restructured from a single large class (1375+ lines) into multiple focused classes organized in logical folders:

### 📁 Folder Structure

```
TableStorage.SourceGenerators/
├── Models/                           # Data models and structures
│   ├── ClassToGenerate.cs           # Core data models for generation
│   └── ModelContext.cs              # Context information for code generation
│
├── Utilities/                        # Helper and utility classes
│   ├── ConfigurationHelper.cs       # Configuration extraction utilities
│   ├── TypeHelper.cs               # Type-related operations
│   └── ValidationHelper.cs         # Compilation validation utilities
│
├── Generators/                       # Main generation logic
│   ├── ClassProcessor.cs           # Orchestrates class processing
│   │
│   ├── AttributeProcessing/         # Attribute parsing and processing
│   │   ├── AttributeProcessor.cs   # Processes TableStorage attributes
│   │   └── MemberProcessor.cs      # Processes class members
│   │
│   └── CodeGeneration/             # Code generation components
│       ├── CodeGenerationBase.cs  # Base functionality for code generation
│       ├── ModelGenerator.cs      # Main model generation orchestrator
│       ├── FactoryGenerator.cs    # Factory method generation
│       ├── ChangeTrackingGenerator.cs # Change tracking functionality
│       ├── TableEntityGenerator.cs    # Table entity members
│       ├── PropertyGenerator.cs       # Property generation
│       ├── IndexerGenerator.cs        # Indexer implementation
│       ├── ValueConversionGenerator.cs # Type conversion logic
│       └── DictionaryImplementationGenerator.cs # IDictionary implementation
│
├── TableContext/                     # (Existing) Table context generators
├── TableSetModel/                    # (Existing) Model generators
├── Header.cs                        # (Existing) Header utilities
└── TableSetModelGenerator.cs       # Main entry point (refactored)
```

## Design Principles Applied

### 1. **Single Responsibility Principle (SRP)**
Each class now has a single, well-defined responsibility:
- `AttributeProcessor` - Only processes attributes
- `MemberProcessor` - Only processes class members
- `FactoryGenerator` - Only generates factory methods
- `ChangeTrackingGenerator` - Only generates change tracking code
- etc.

### 2. **Separation of Concerns**
Different aspects of code generation are separated:
- **Attribute Processing**: Parsing and extracting information from attributes
- **Code Generation**: Creating the actual C# code
- **Utilities**: Helper functions and validation
- **Models**: Data structures used throughout the process

### 3. **Open/Closed Principle**
The architecture allows for easy extension:
- New generators can be added to the `CodeGeneration` folder
- New attribute processors can be added to the `AttributeProcessing` folder
- The main orchestrator (`ModelGenerator`) coordinates all generators

### 4. **Dependency Inversion**
Higher-level modules (like `ModelGenerator`) depend on abstractions rather than concrete implementations, making the code more maintainable.

### 5. **Clear Naming and Documentation**
- All classes, methods, and properties have descriptive names
- XML documentation comments explain the purpose and usage
- Folder structure clearly indicates the purpose of each component

## Benefits of the Refactoring

### For Developers
- **Easier to Navigate**: Related functionality is grouped together
- **Easier to Debug**: Smaller, focused classes are easier to understand
- **Easier to Test**: Each component can be tested in isolation
- **Easier to Modify**: Changes to one aspect (e.g., change tracking) don't affect other areas

### For Maintainability
- **Reduced Complexity**: No more 1375-line mega-class
- **Better Code Reuse**: Common functionality is extracted to utilities
- **Consistent Structure**: Similar patterns across all generators
- **Clear Dependencies**: Easy to see what depends on what

### For Extensibility
- **New Features**: Easy to add new generators or processors
- **Customization**: Individual components can be customized without affecting others
- **Future-Proof**: Architecture can evolve without major rewrites

## Usage

The main entry point remains the same - `TableSetModelGenerator` implements `IIncrementalGenerator`. However, it now delegates to specialized classes:

1. **Configuration** is handled by `ConfigurationHelper`
2. **Validation** is handled by `ValidationHelper`
3. **Class Processing** is handled by `ClassProcessor`
4. **Code Generation** is orchestrated by `ModelGenerator`

## Key Classes

### TableSetModelGenerator
The main entry point that coordinates the entire generation process. Now clean and focused on orchestration rather than implementation details.

### ClassProcessor
Processes class declarations and extracts all necessary information for code generation.

### ModelGenerator
Orchestrates all the specialized generators to produce the final C# code.

### Specialized Generators
Each generator focuses on a specific aspect:
- Factory methods, blob support, change tracking, properties, indexers, etc.

This architecture makes the codebase much more maintainable and easier to work with for developers.
