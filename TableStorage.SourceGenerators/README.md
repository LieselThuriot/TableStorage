# TableStorage Source Generators - Clean Architecture

This directory contains the refactored TableStorage source generators that follow good coding practices and proper separation of concerns.

## Architecture Overview

The code has been restructured from large monolithic classes into multiple focused classes organized in logical folders with clear separation between different types of generation:

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
├── Generators/                       # Main generation logic (organized by purpose)
│   │
│   ├── TableSetGeneration/          # TableSet model generation
│   │   ├── ClassProcessor.cs       # Orchestrates class processing
│   │   │
│   │   ├── AttributeProcessing/     # Attribute parsing and processing
│   │   │   ├── AttributeProcessor.cs # Processes TableStorage attributes
│   │   │   └── MemberProcessor.cs    # Processes class members
│   │   │
│   │   └── CodeGeneration/          # Code generation components
│   │       ├── CodeGenerationBase.cs  # Base functionality for code generation
│   │       ├── ModelGenerator.cs      # Main model generation orchestrator
│   │       ├── FactoryGenerator.cs    # Factory method generation
│   │       ├── ChangeTrackingGenerator.cs # Change tracking functionality
│   │       ├── TableEntityGenerator.cs    # Table entity members
│   │       ├── PropertyGenerator.cs       # Property generation
│   │       ├── IndexerGenerator.cs        # Indexer implementation
│   │       ├── ValueConversionGenerator.cs # Type conversion logic
│   │       └── DictionaryImplementationGenerator.cs # IDictionary implementation
│   │
│   └── TableContextGeneration/      # TableContext DI generation
│       ├── TableContextClassProcessor.cs  # Processes context class declarations
│       ├── TableContextValidator.cs       # Validates dependencies and capabilities
│       ├── TableContextGenerator.cs       # Main generation orchestrator
│       ├── ServiceExtensionGenerator.cs   # Service registration extensions
│       ├── ConstructorGenerator.cs        # Constructor generation
│       ├── ServiceRegistrationGenerator.cs # DI registration methods
│       ├── HelperMethodGenerator.cs       # Helper methods (GetTableSet, etc.)
│       ├── FieldGenerator.cs              # Private field generation
│       └── README.md                      # TableContext-specific documentation
│
├── TableContext/                     # (Existing) Table context generators
├── TableSetModel/                    # (Existing) Model generators
├── Header.cs                        # (Existing) Header utilities
├── TableSetModelGenerator.cs       # Main entry point for TableSet generation (refactored)
├── TableContextGenerator.cs        # Main entry point for TableContext generation (refactored)
└── README.md                       # This file
```

## Design Principles Applied

### 1. **Single Responsibility Principle (SRP)**
Each class now has a single, well-defined responsibility:
- `AttributeProcessor` - Only processes attributes
- `MemberProcessor` - Only processes class members
- `FactoryGenerator` - Only generates factory methods
- `ChangeTrackingGenerator` - Only generates change tracking code
- etc.

### 2. **Separation of Concerns by Purpose**
Different types of generation are clearly separated:
- **TableSetGeneration**: Everything related to generating table entity models
  - Attribute processing, code generation, class processing
- **TableContextGeneration**: Everything related to generating DI context classes
  - Service registration, constructor injection, helper methods

### 3. **Clear Namespace Organization**
Namespaces directly reflect the folder structure:
- `TableStorage.SourceGenerators.Generators.TableSetGeneration.*`
- `TableStorage.SourceGenerators.Generators.TableContextGeneration.*`

### 4. **Reuse and Consistency**
- Common utilities are shared between both generation types
- Similar patterns used across both `TableSetGeneration` and `TableContextGeneration`
- Consistent naming conventions and documentation standards

## Benefits of the Clean Organization

### For Developers
- **Easy Navigation**: TableSet and TableContext generation are clearly separated
- **Clear Purpose**: Each folder has a specific, well-defined purpose
- **Easy to Debug**: Smaller, focused classes are easier to understand
- **Easy to Test**: Each component can be tested in isolation
- **Easy to Modify**: Changes to TableSet generation don't affect TableContext generation

### For Maintainability
- **Reduced Complexity**: No more large monolithic classes
- **Better Code Reuse**: Common functionality is extracted to utilities
- **Consistent Structure**: Similar patterns across both generation types
- **Clear Dependencies**: Easy to see what depends on what
- **Modular Architecture**: Each type of generation is self-contained

### For Extensibility
- **New Features**: Easy to add new generators for either TableSet or TableContext
- **Customization**: Individual components can be customized without affecting others
- **Future-Proof**: Architecture can evolve without major rewrites
- **Type-Specific Extensions**: Easy to extend TableSet generation independently from TableContext

## Key Architectural Decisions

### 🏗️ **Separation by Generation Purpose**
Instead of organizing by technical concerns (all processors together, all generators together), we organize by business purpose:
- **TableSetGeneration**: Everything needed to generate table entity models
- **TableContextGeneration**: Everything needed to generate dependency injection contexts

### 🔄 **Shared Infrastructure**
Common concerns are shared via the `Models/` and `Utilities/` folders:
- Data structures (`ClassToGenerate`, `ModelContext`)
- Helper functions (`ValidationHelper`, `ConfigurationHelper`)
- Base functionality (`CodeGenerationBase`)

## Usage

Both generators maintain 100% backward compatibility:

### 📋 **TableSetModelGenerator**
- Processes classes marked with `[TableSet]` attributes
- Generates table entity models with properties, indexers, and change tracking
- Main entry point delegates to `TableSetGeneration.*` classes

### 🏗️ **TableContextGenerator** 
- Processes classes marked with `[TableContext]` attributes
- Generates dependency injection contexts with service registration
- Main entry point delegates to `TableContextGeneration.*` classes

### 🔧 **Shared Infrastructure**
- `Models/`: Data structures used by both generators
- `Utilities/`: Helper functions and validation logic
- Both generators reuse common patterns and utilities

## Migration Impact

✅ **Zero Breaking Changes**: All existing code continues to work exactly as before
✅ **Same Generated Code**: Output is identical to the previous implementation  
✅ **Same Performance**: No performance impact from the refactoring
✅ **Better Maintainability**: Much easier for developers to work with the codebase

This architecture provides a solid foundation for future development while making the current codebase much more maintainable and developer-friendly.
