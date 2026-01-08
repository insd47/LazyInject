# Changelog

## [1.0.0] - 2026-01-08

### Added
- Initial release
- `[Inject]` attribute for field-based dependency injection
- Source Generator for lazy getter property generation
- `DIContainer` static container for dependency registration
- Support for keyed dependencies via `[Inject("key")]`
- Field accessibility preservation (private, protected, internal, public)
- Compile-time analyzer to prevent direct field access (INJECT001)
