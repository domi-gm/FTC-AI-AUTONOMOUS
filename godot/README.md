# FTC Simulator — Godot + C#

Simulator local 3D inspirat de Turtle Sim. Deschide project.godot în Godot
4.7.2 .NET, compilează și apasă F5. Scena principală: Scenes/Simulator.tscn.
Multiplayerul online nu face parte din scop.

Comenzi:
- WASD: mișcare relativă la cameră; Q/E: rotirea robotului.
- Mouse dreapta + deplasare: rotirea camerei; rotiță: zoom; C: schimbă vederea.
- Shift/J: colectare; Space: lansare; T: țintă HIVE/flower.
- H: human player; K: extragere din flower.
- Practice: B/N adaugă pollen/nectar; F2 editare traseu; click adaugă punct;
  Backspace șterge ultimul punct; P urmărește traseul.
- Escape: pauză; R: reset; F5/F9: salvează/încarcă starea.

Datele persistente sunt în user:// din Godot. Vechea scenă Main.tscn și
instrucțiunile din docs/GRID_TUTORIAL.md rămân disponibile.

Verificare: dotnet build, apoi Godot cu
--headless --path <folder-proiect> -- --smoke-test.
Testul verifică orientarea camerei, distribuția inițială, lansarea liberă și
blocată, coliziunile, colectarea, pauza, restaurarea, HIVE și resetarea.

Explicații și limite: docs/EXPLICATIE_SIMULATOR_3D.md.
Implementarea este încă o aproximare, nu o reproducere identică sau un arbitru
oficial de concurs.
