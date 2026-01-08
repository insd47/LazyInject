# LazyInject

A lightweight dependency injection framework for Unity using C# Source Generators.

## Features

- **Lazy Loading**: Dependencies are resolved only when first accessed
- **Compile-time Generation**: No runtime reflection overhead
- **Simple API**: Just add `[Inject]` to your fields
- **Unity 6+ Compatible**: Uses Roslyn Source Generators

## Installation

### Via Package Manager (Git URL)

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Enter: `https://github.com/insd47/LazyInject.git`

### Via manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "dev.insd.lazyinject": "https://github.com/insd47/LazyInject.git"
  }
}
```

## Usage

### 1. Register Dependencies

```csharp
public class GameManager : MonoBehaviour
{
    void Awake()
    {
        // Register singleton services
        DIContainer.Global.Register<PlayerState>(new PlayerState());
        DIContainer.Global.Register<ILogger>(new GameLogger());

        // Register with key
        DIContainer.Global.Register<ILogger>(new DebugLogger(), "debug");
    }
}
```

### 2. Inject Dependencies

```csharp
public partial class PlayerController : MonoBehaviour
{
    [Inject] private PlayerState _playerState;
    [Inject("debug")] private ILogger _logger;

    void Start()
    {
        // Use generated properties
        dPlayerState.Initialize();
        dLogger.Log("Player initialized");
    }
}
```

### Important Notes

- Classes using `[Inject]` must be declared as `partial`
- Access dependencies via generated **Properties**, not fields
- Field accessibility is preserved (private field → private property)
- Returns `null` if dependency is not registered (no exception thrown)
- **Recommended**: Use `d` prefix in property names (e.g., `dPlayerState`) to avoid confusion with type names

## Generated Code Example

For this field:

```csharp
[Inject] private PlayerState _playerState;
```

The Source Generator creates:

```csharp
private PlayerState dPlayerState
{
    get
    {
        if (_playerState == null)
        {
            _playerState = DIContainer.GetValue(typeof(PlayerState), "") as PlayerState;
        }
        return _playerState;
    }
}
```

## Diagnostics

| Code   | Severity | Description                                                                           |
| ------ | -------- | ------------------------------------------------------------------------------------- |
| LDI001 | Error    | Direct access to `[Inject]` field is not allowed. Use the generated property instead. |
| LDI002 | Warning  | Generated property name matches type name. Consider using `d` prefix to avoid confusion. |

## Building from Source

The Source Generator project is in the `.SourceGenerator/` folder (hidden from Unity).

### Using the build script

```bash
./build.sh
```

This will build the DLL and copy it to the `Plugins/` folder.

### Manual build

```bash
cd .SourceGenerator
dotnet build -c Release
cp bin/Release/netstandard2.0/Inject.CodeGen.dll ../Plugins/
```

## License

MIT License
