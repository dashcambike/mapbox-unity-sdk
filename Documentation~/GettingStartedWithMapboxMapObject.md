
# Getting Started with Mapbox Map Script and Object

In this guide, you’ll learn how to set up and interact with the Mapbox Map object in Unity.  
We’ll explore how the map system works, how the related scripts are structured, and how to properly access the `MapboxMap` instance in your own code.

---

## Understanding the `MapboxMap` Object

The `MapboxMap` class is the root object that controls the map instance.  
It’s a plain C# class — meaning it does not inherit from Unity’s `MonoBehaviour`.  

Because of this, you cannot attach it directly to a GameObject or interact with it in the Unity Editor.

---

## Exposing the Map in Unity: `MapboxMapBehaviour`

To expose `MapboxMap` in Unity, a helper interface class is used: `MapboxMapBehaviour`.  

This class:
- Inherits from `MapBehaviourCore`,  
- Which in turn inherits from `MonoBehaviour`.

That means `MapboxMapBehaviour` can be attached to a Unity GameObject and used directly in the Unity Scene or Inspector.

---

## How It Works

The classes `MapBehaviourCore` and `MapboxMapBehaviour` are responsible for:

1. Creating a `MapboxMap` instance using the settings configured in the Unity Inspector.  
2. Exposing the map object after it’s created, allowing other scripts to access it later.

After initialization, these classes mainly serve as a reference to retrieve the existing `MapboxMap` instance.

---

## In the Scene

When you open a demo scene, you’ll notice a GameObject named `Map`.  
This object has a `MapboxMapBehaviour` script attached.

During scene startup (in Unity’s `Start()` lifecycle method), this script automatically:
- Creates the `MapboxMap` object,
- Initializes it based on your scene’s configuration,
- Makes it available through the property:  

```csharp
MapboxMapBehaviour.MapboxMap
```

---

## The Initialization Process

By default, map initialization starts during Unity’s built-in `Start()` phase.

However, you can change this behavior — for example, to manually control when the map initializes — by adjusting the initialization settings in the Inspector.

---

## Accessing the Map Object in Your Own Scripts

If you want to use the map object in your own code (for example, to query tiles, add layers, or respond to map events), the recommended approach is to:

1. Reference the `MapBehaviourCore` component in your script.  
2. Subscribe to its `Initialized` event.  
3. Store the resulting `MapboxMap` instance when it becomes available.

Here’s a simple example:

```csharp
using UnityEngine;
using Mapbox.Unity.Map;

public class Test : MonoBehaviour
{
    public MapBehaviourCore MapCore;
    private MapboxMap _map;

    public void Awake()
    {
        MapCore.Initialized += (map) =>
        {
            _map = map;
            Debug.Log("Map is initialized");
        };
    }
}
```

## What This Does
- The script listens for the map’s initialization event.  
- When the map is ready, it assigns it to the private `_map` field.  
- You can then safely use `_map` for all your map-related logic.

---
