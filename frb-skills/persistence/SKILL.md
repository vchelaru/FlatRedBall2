---
name: persistence
description: "Saving and loading game data with Engine.Storage, same code on desktop and web. Triggers: save game, high score, settings file, StorageName, IStorageBackend, localStorage, IsPersistent."
---

# Persistence

`Engine.Storage` (`GameStorage` in `src/IO`) is the save/load API: async, string-keyed, same code on desktop and web, so no `System.IO.File` or `#if`. The XML docs on `GameStorage` and `IStorageBackend` list the calls.

- **Set `EngineInitSettings.StorageName`.** If it is unset, the first storage call throws on every platform.
- **`OpenWriteAsync` stores nothing until `CommitAsync`.** Disposing without committing discards the write.
- **Desktop** writes files under a per-user folder (`StorageRootDirectory` overrides it). **Web** uses `localStorage`: a write past its size limit throws `StorageException`, and `Engine.Storage.IsPersistent` is false if the browser blocks it.
- **Tests:** a bare `FlatRedBallService` uses `InMemoryStorageBackend`. Assign `Engine.Storage.Backend` after `Initialize` to swap it.
