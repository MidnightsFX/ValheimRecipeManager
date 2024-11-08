 **0.4.0**
---
```
- Add support for piece modifications
	- Details about pieces can now be modified
	- Piece modifications are seperated from item modifications
- Added support for multiple config files of each type
- Unified synchronization for multiple files & piece or recipe types
- Virtualized piece and recipe synchronization, files are no longer created or mirrored from the host/server
```

 **0.3.4**
---
```
- Bog Witch Update
- Jotunn Updated to 2.21.2
```

 **0.3.1**
---
```
- Modify ensures the target recipe does not already exist
```

 **0.3.0**
---
```
- Added more error reporting for common recipe issues
- Updated example docs
- Server sync'd recipes are now virtual
```

 **0.2.0**
---
```
- Improves ways to reference a recipe as handcrafted
- Added recipe state sychronization on config file server sync
```

 **0.1.3**
---
```
- Fixed server sync'd configurations trying to write out to a temp address
```

 **0.1.2**
---
```
- Fixed customRPC naming to not collide with ValheimFortress
```

 **0.1.1**
---
```
- Added support for targeting recipe names. Allows mutation of multiple recipes that target the same prefab (eg craft x5)
- Added recipe reversion which is applied right before reloading recipes to ensure previous recipe modifications are removed before reapplying
```

 **0.1.0**
---
```
- Initial release, I'm sure I forgot something
```