# Bachelor Project: Automatic 3D Scene Generation using LLM
This application has been developed as part of my bachelor's thesis. The application is a framework for generating 3D scenes using and LLM which takes only 2 inputs, those are a text with the context of the place and an optional language input where the default language is german, this makes the aim of the scenes generated to help with language learning by seeing different places names using in a different language with its translation. 
The application introduces 6 modules, each module contributes to the final generated scene, those modules are chosen based on the main requirements that all places should be built of, like the floor plan, walls, doors, objects, materials, layouts, and more and more details.

## Installation
### Prerequisites
- Unity

### Setup
1.  **Download the repository**:
```bash
git clone <repository_url>
```
or
Download project zip file
Extract the project from it

2. **Add the Unity Project**:
From Unity Hub, click on the dropdown Add -> Add project from disk
Select the downloaded project folder

3. **Open the Unity Project**:
Open the project from Unity Hub, it will take sometime
from inside Unity, through the asset manager go to Assets -> scenes
open the "SecondScene.unity" scene

## Usage
#### Opening Main file:
* Through the asset manager go to Assets -> SceneGeneration
* Open SceneCreator.cs using any IDE

#### Setting the inputs for the LLM:
* In the main SceneCreator file. Go to the Start Method in line 36.
* Type the place you want to generate in the area variable
* Optionally change the language to the language you want

#### Generate Scene.
* Go back to Unity
* Run the application

## Modules
1.  **Area Module**:
- Initiates the process by sending the first prompt to Gemini.
- The module's prompt matches multiple patterns to get an efficient output from the LLM
- Takes the LLM response, decodes it, and saves it to a hierarchical datatype called ZoneNode

**Note**: All the modules depend on that decoded response from the LLM.

2.  **Textures Module**:
- Takes all the texture names given by the LLM through the decoded ZoneNode datatype.
- Searchs for the most suitable texture from the AmbientCG textures CSV file using Fuzzy Algorithm using Fuzzy Sharp Library.
- Downloads the texture if not downloaded.
- Makes a material for that texture if not already made.
- Sets all the zones floor and wall materials in the ZoneNode Datatype.

3.  **Floor Plan Module**:
- From the root ZoneNode object, uses its hierarchical structure (children), name, translatedName, size, side, type, height, and floorMaterial Variables.
- Generates the platforms(floor) for each zone using Improved BSP Algorithm.
- Generates the ceiling for each zone.
- Generates the names apove each zone.
- Applys the materials for the platforms of each zone.

4.  **Connections Module**:
- From the ZoneNodes, uses all their platforms and wall materials.
- Calculates adjacencies and connections between all platforms.
- Sends a prompt to Gemini asking for the connection types (open, wall, door) of the calculated connections.
- Decodes the reponse of the LLM.
- Builds the connections based on the connection types provided by the LLM.
- Applies wall material to the connections built.

5.  **Assets Selection Module**:
- From the ZoneNodes, uses all their objects variables.
- Searchs for the assets in Sketchfab by quering their names.
- Specifies all important assets annotations.
- Sends a prompt to the LLM asking for the most suitable asset for that specific object based on the assets annotations.
- Decodes the LLM selected assets response.
- Downloads the selected assets if not already downloaded.
- Saves the selected assets to the local storage if not already saved.
- Saves the reference to the assets for each ZoneNode.

6.  **Layout Module**:
- For each zone, use its platform, neighbors connection types, and the assets selected.
- Sends a prompt with platform sizes, connection areas, and zone assets to the LLM asking for building a layout.
- Decodes LLM response.
- Imports assets.
- Positions them.
- Rotates them.

## Asset Manager Structure
-  `Assets/PlayerObject/`: Contains the script for the mouse and keyboard player code.
-  `Assets/SceneGeneration/`: Contains the whole framework code with a .asmdef for dependencies managing.
-  `Assets/SceneGeneration/SketchFabModels/`: Contains the previously downloaded models from Sketchfab.
-  `Assets/SceneGeneration/Texture/`: Contains the previously downloaded textures from AmbientCG along with the generated materials.
-  `Assets/SceneGeneration/Texture/ambientCG_Textures.csv`: The Textures datasset used for the Floor and wall materials generation.
-  `Assets/SceneGeneration/Texture/ZoneNode.cs`: The main and DataType that represents almost all the scene.
-  `Assets/SceneGeneration/Texture/Connection.cs`: The datatype for the connections between zones.
-  `Assets/SceneGeneration/Texture/LLMAPI.cs`: An abstract datatype for different LLMs APIs.
-  `Assets/SceneGeneration/Texture/GeminiAPI.cs`: The main LLM API class.
-  `Assets/SceneGeneration/Texture/GeneralScript.cs`: The general functions used across all the framework's scripts.
-  `Assets/SceneGeneration/Texture/ModelRetriever.cs`: An abstract datatype for the assets retrieving process.
-  `Assets/SceneGeneration/Texture/SketchfabRetrivier.cs`: The main used assets retrieving class in the framework.
-  `Assets/SceneGeneration/Texture/SceneCreater.cs`: The main script for the whole process generation.
-  `Assets/SceneGeneration/Texture/TextureSelector.cs`: The textures managing class.
-  `Assets/Scripts/`: Implemented but unused scripts due to the time constraints and their unrelevancy to the Bachelor Thesis Aim.

## Notes
This project was developed as a bachelor thesis project. Contributions are not provided.

## Author
This project was developed by Mostafa Hossam Eldin Mostafa Ramadan.
