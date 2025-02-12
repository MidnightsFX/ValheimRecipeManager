 **0.4.2**
---
```
- Fixes duplicate recipes when recipes are modified, and modified again. Primarily during multiple world initializations or character swapping worlds.
- Fixes items with no crafting station being unrepairable in specific cases
```

 **0.4.1**
---
```
- Adds support for nocost recipes
- Adds separate repair station configuration for recipes, if unset defaults to the crafting station
- Removed recipe refund setting as it was not working as intended
```

 **0.4.0**
---
```
- Add support for piece modifications
	- Details about pieces can now be modified
	- Piece modifications are separated from item modifications
- Added support for multiple config files of each type
- Unified synchronization for multiple files & piece or recipe types
- Virtualized piece and recipe synchronization, files are no longer created or mirrored from the host/server
- Some improvements to recipe modification synchronization
```

 **0.3.4**
---
```
- Added guards to do nothing if recipe reload is called when you have no recipes set to modify
```

 **0.3.3**
---
```
- Simplified item add during server handshake post config received
```

 **0.3.2**
---
```
- More eventual consistency checks
- Add ensures that the recipe does not already exist
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