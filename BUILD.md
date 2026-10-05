# Building the source

Users do not need to compile. The release DLL is the exact pinned, tested pack DLL; it is not rebuilt during publication.

Developers need .NET SDK 8 and a local Sprocket 0.2.55.5 loader installation with generated interop. Build the root csproj with dotnet build -c Release -p:GameDir=YOUR_GAME_DIRECTORY. Keybind consumers require the shared API DLL in that installation. All game/loader references use Private=false. No game binaries or generated caches are distributed. Run the supplied pure tests where present; they do not prove native gameplay. Keep code/runtime changes separate from personalized config edits.
