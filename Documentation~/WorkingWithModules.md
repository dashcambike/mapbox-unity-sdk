## Working with layer modules


Layer modules are main systems corresponding to Mapbox api endpoints, like static api, terrain api and vector api.
Orchestrated by the map object; they request, process and visualize data to decorate the map.
Three main elements important in this section are (1) base map system, (2) static api layer module, (3) terrain layer module and (4) vector module.

### creating the base map
To be able to use modules and create a map visual, we’ll first need a base map system.
We'll need a gameobject with MapboxMapBehaviour script attached. Script should come with a few default settings but if it doesn't (in earlier versions of the SDK), change some basic values as follows;
Pitch: 90, Scale: 1, Zoom: 16, Latitude Longitude: Any location you want

If you run the application now, only with this base map script, you should see a bunch of empty tiles with various sizes created in the scene.

Now that we have a map, we can add modules to decorate it. this base map script will look for any layer module scripts on the same gameobject as itself and add them to its update cycle. 

![Base map script output](Images/WorkingWithModulesBaseMap.png)

### Static Api Layer Module
Static Api Layer module, as the name suggests, utilizes the [Mapbox Static Images Api](https://docs.mapbox.com/api/maps/static-images/) which serves server-side created images of requested region to clients.

Static api layer module will request and download images necessary to fill up the requested area. Then these images will be passed to tile materials, which by default are using a custom shader and texture fields, to render it on the mesh.

If tile material and shader is changed (via the optional tile creator script), static api layer module will not be able to find the relevant shader field and images might not be applied as intended.

![Static api layer module output](Images/WorkingWithModulesStaticApi.png)


### Terrain Layer Module
Terrain layer module utilizes the [Mapbox Terrain-Rgb api](https://docs.mapbox.com/data/tilesets/reference/mapbox-terrain-rgb-v1/) which serves pre-generated global elevation data encoded in PNG files as color values.

Terrain layer module will request and download images necessary to create a terrain for current map view. These images then either passed directly to material to generate elevation on GPU-side or processed on CPU-side to create a 3d mesh of the area. Both options has advantages and disadvantages of their own.

Terrain layer module only supports the `rgb v1` api at the moment and support for `dem v1` will be added in the future.

Similar to static api layer module, terrain module (in shader elevation mode) also relies on a specific terrain material and shader to work as intended.

![Static api with terrain output](Images/WorkingWithModulesTerrain.png)


### Vector Module
Vector module utilizes the [Mapbox Vector Tiles api](https://docs.mapbox.com/api/maps/vector-tiles/) which serves vector tiles. `Street V8` is the main dataset this module targets and you can find the details on its data and how it’s structured in [Mapbox Streets v8 documentation](https://docs.mapbox.com/data/tilesets/reference/mapbox-streets-v8/).

Vector layer module will download raw vector data and through the "layer visualizer" scripts (submodules), it creates visuals. Most common example of this is the 3d buildings (extruded polygons).

Vector layer module request, decompress and ready the data but doesn’t process or create visuals by itself. It’ll only produce visual results once submodules called `Layer Visualizer` are added. You can find example visualizers in the samples included into the package.

![Vector module output](Images/WorkingWithModulesVector.png)
