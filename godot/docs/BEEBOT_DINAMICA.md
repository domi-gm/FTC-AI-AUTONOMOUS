# Beebot în simulator: import și dinamică

Codul pornește de la commit-ul GitHub `1477ffe2adb517e3df520d53fa5b106a94467606` din `AI-Practice-grounds-setup`. Commit-ul `88d32b0` din `feature/cad-robot-import-joint-controls` este strămoș al acestui branch; importul URDF și controalele articulațiilor erau deja incluse. Proiectul local este `C:/Users/Patric/Documents/ftc-simulator`. Modificările pentru dinamică sunt în checkout-ul `.publish-ftc` și în copia locală actualizată. Python Field + Visuals și copiile existente din `backups/` sunt păstrate.

## Fișiere și utilizare

Importă `D:/beebot-urdf-work/beebot_sim_description/urdf/beebot.urdf` din Robot Workshop → Import CAD Robot → Import URDF. Păstrează întregul pachet, inclusiv folderul meshes. Revizuiește preview-ul, apoi SAVE + APPLY ACTIVE. În Practice, WASD deplasează robotul, Q/E îl rotește. L deschide/închide bariera la 0/90°, cu un motor care ține poziția în limita cuplului. I selectează un motor de mecanism, U/O îl comandă; roțile de tracțiune sunt comandate de conducerea mecanum.

Pachetul are toate cele 27 link-uri și 26 articulații, fără obiectele de joc. Varianta pentru simulator are 1.995.967 triunghiuri; microdetaliile au fost simplificate pentru afișare. Mesh-urile și arhiva Fusion originale sunt păstrate. Cutia fiecărui mesh simplificat a fost comparată cu sursa; diferențele extremelor sunt sub 2 mm. Aceasta verifică dimensiunile, nu garantează păstrarea fiecărui șurub sau spike.

## Ce simulează acum

Masa, centrul de masă și tensorul de inerție sunt citite din URDF. Tensorul descrie rezistența la rotire, inclusiv distribuțiile oblice; de exemplu un braț lung se rotește mai greu decât o piesă compactă cu aceeași masă. Pentru Godot, tensorul este descompus în axe principale; corpurile fizice sunt orientate pe acele axe, iar mesh-urile și reperele articulațiilor sunt transformate invers, astfel încât robotul nu-și schimbă forma sau poziția inițială.

Articulațiile folosesc amortizarea proporțională cu viteza și frecarea opusă mișcării din `<dynamics>`. Motorul barierei respectă viteza și cuplul din `<limit>`. Șasiul Beebot este un RigidBody3D: motoarele aplică cuplu roților, iar tracțiunea și alunecarea generează forțele care deplasează robotul. Greutatea, coliziunile și reacțiile articulațiilor acționează fizic. Modelele vechi fără extensia de mecanum păstrează conducerea cinematică.

`<ftc_simulator>` este extensia acestei aplicații, nu un plugin ROS/Gazebo. Ea specifică roțile, orientarea rolelor, modelul motorului și butoanele articulațiilor. Motoarele folosesc o aproximare liniară cuplu–viteză. Roțile au coliziuni cilindrice și tracțiune anisotropă (forța este diferită în direcția rolelor). Nu se simulează individual cele câteva mii de spikes. Restul pieselor folosesc coliziuni aproximative cu cutii; pentru șasiul fizic, suprafețele lor sunt retrase cu 2 mm.

## Parametri verificați și estimați

În CAD apar patru SWYFT Drive V2, cu motor SR-MOTOR-DC-01. Producătorul publică 6000 RPM la motor, reducție 12,7:1 și cuplu maxim 1,47 kg·cm. Acestea dau aproximativ 49,47 rad/s la ieșire; cu eficiența estimată de 85%, cuplul de ieșire folosit este 1,556 Nm. Sursa: [SWYFT Drive V2](https://swyftrobotics.com/kits/swyft-drive-v2).

CAD-ul mai conține REV HD Hex și cartușe UltraPlanetary 3/4/5. REV publică 6000 RPM și 0,105 Nm pentru motorul fără reductor; rapoartele reale ale cartușelor sunt 2,89, 3,61 și 5,23. Surse: [motorul REV](https://www.revrobotics.com/REV-41-1291/) și [desenul cartușelor](https://www.revrobotics.com/content/docs/REV-41-1601-1603-DR.pdf). Asocierea exactă motor–mecanism nu este confirmată; nu am aplicat aceste valori automat la barieră sau lame.

Masa inițială este estimarea CAD de 26,414 kg, care include toate variantele ascunse cerute. Ea poate depăși masa robotului asamblat în configurația reală. Eficiența reductorului, aderența, rigiditatea contactului rolelor și parametrii actuatoarelor mecanismelor sunt estimări editabile. Nu sunt măsurători ale robotului real. Shooterul și grupurile fără articulație principală identificată rămân rigid conectate; lansarea mingilor continuă să folosească modelul balistic al aplicației. Nu este reconstituită transmisia mecanică internă a shooterului.

Editează `D:/beebot-urdf-work/dynamics_parameters.json`, apoi rulează `python D:/beebot-urdf-work/add_simulator_dynamics.py` și reimportă URDF-ul. Schimbarea masei totale scalează proporțional și inerțiile; centrul de masă rămâne același. Păstrează copii ale configurațiilor calibrate. Generatorul recitește sursa originală, deci rulările repetate nu multiplică din nou masa.

## Cauză, reproducere și verificare

Inițial, importatorul ignora COM, inerția și `<dynamics>`. Beebot-ul complet depășea limita veche de 200.000 triunghiuri; limitarea este acum la 2 milioane total și 32 MB per STL. Pachetul complet de 31,5 milioane triunghiuri rămâne destinat CAD-ului și aplicațiilor care pot suporta această complexitate.

Un alt defect reproductibil: robotul Beebot stătea pe podea, motoarele roților primeau cuplu, dar nu se deplasa. Cutiile aproximative ale lamelor de intake atingeau solul înaintea roților și produceau frecare artificială. Retragerea suprafețelor de coliziune cu 2 mm a eliminat contactul fals; gravitația și coliziunile cu solul rămân active.

Dimensiunile erau recalculate pentru toate cele milioane de vârfuri de fiecare dată când era cerut bounding box-ul unei piese. Acum se memorează cutia sursă a mesh-ului după hash, iar pentru conversiile de unități și rotațiile cu 90° se transformă exact cele opt colțuri. Schimbarea mesh-ului schimbă hash-ul și nu reutilizează cutia veche.

Verificări executabile cu Godot 4.7.2 .NET:

- `dotnet build`: compilarea codului.
- `--urdf-test`: 32 verificări, inclusiv COM rotit, tensor complet, amortizare și mișcare fizică la limite.
- `--joint-controls-test`: 30 verificări ale tastelor, butoanelor și restaurării țintelor.
- `--mechanism-test`: 22 verificări ale mecanismelor.
- `--stl-test`: import, scări, ownership și preview.
- `--physical-drive-test`: 9 verificări ale șasiului fizic și snapshot-urilor.
- `--robot-import-test D:/beebot-urdf-work/beebot_sim_description/urdf/beebot.urdf`: 27 verificări pe Beebot complet: import/persistență, însumarea maselor pentru link-uri fixed, repaus fără derivă, snapshot-uri, stabilitate sub gravitație, înainte/lateral/rotire, motorul barierei și ambele ținte L, intake pornit/oprit, colectare și inventar plin, direcții WASD fixe și joystick. Raportul este `runtime_validation.json` lângă URDF.

Aceste teste verifică implementarea și robotul estimat; nu demonstrează calibrarea cu robotul real. Pentru aceasta sunt necesare măsurători de masă, accelerație, alunecare și viteza mecanismelor.

Piesele conectate cu articulații fixed sunt reunite în același corp fizic pentru modul mecanum. Cele 27 link-uri CAD rămân în profil și toate mesh-urile sunt afișate; în motorul fizic există nouă corpuri rigide. Masa, centrul de masă și inerția agregată includ fiecare piesă fixă, cu termenii de translație ai teoremei axelor paralele. Repausul Beebot-ului a fost verificat pe cinci secunde simulate: deriva raportată a fost zero. Corpurile separate și multele constrângeri fixed produceau anterior deplasare artificială, deși comanda motoarelor era zero.
# Corecție intake — 13 octombrie 2026

Reproducere: în Free Practice, pornești intake-ul cu J sau Shift. În versiunea anterioară colectarea din joc era activată, dar paletele CAD nu se roteau: articulațiile nu aveau o legătură cu starea `RobotAgent.Intake`.

Acum extensia URDF `ftc_simulator` conține `<intake_motor joint="intake_blader_joint" power="1" />` și `<intake_motor joint="intake_bladel_joint" power="-1" />`. Importatorul păstrează puterea semnată în `IntakePower`; motorul rulează la puterea configurată când intake-ul este ON și frânează când este OFF. Paletele se rotesc în sensuri opuse, spre centru. Selectarea unei articulații pentru control manual nu mai anulează comanda automată când U/O nu sunt apăsate. U/O permit în continuare mișcare manuală cât timp sunt ținute apăsate.

În timpul conducerii apare butonul **[J / Shift] INTAKE: OFF/ON**, cu inventarul, de exemplu `1/4`. J, tasta configurată de intake sau clic pe buton schimbă starea; în pauză comanda este blocată. Pentru colectare, apropie partea frontală de piesele aflate pe podea; capacitatea este de patru piese.

În sesiunea verificată robotul pornea cu **4/4 piese preîncărcate**. Acesta este și motivul pentru care colectarea nu putea adăuga alte piese. Butonul indică acum explicit **FULL (4/4) — shoot first**. Lansează o piesă pentru a face loc sau configurează un inventar inițial gol din managerul de poziții și inventar. Paletele pot rula și când inventarul este plin, dar colectarea nu depășește capacitatea.

`add_simulator_dynamics.py` generează legăturile pentru importuri viitoare. `D:\beebot-urdf-work\update_intake_profile.py` actualizează profilul Beebot existent și intrarea lui din biblioteca de roboți, păstrând originalele într-un director datat `profiles-before-intake-fix`.

Verificări noi pe robotul real importat: legăturile supraviețuiesc salvării JSON, butonul pornește intake-ul și afișează ON, ambele palete ating aproximativ +5/-5 rad/s, OFF le oprește, o piesă de pe podea rămâne pe teren când intake-ul este oprit și este colectată exact o dată când pornește. Colectarea rămâne modelul de joc bazat pe o zonă frontală; nu reprezintă o simulare completă a deformării rolelor și transportului piesei prin mecanism.
# Direcții WASD fixe — 13 octombrie 2026

Anterior, modul implicit transforma WASD după orientarea robotului. Celălalt mod transforma comenzile după unghiul camerei: după o rotație, W putea conduce spre altă latură a terenului.

Modul implicit este acum **Field-Centric**: W = −Z, S = +Z, A = −X, D = +X în coordonatele terenului. Rotația robotului și schimbarea/rotirea camerei nu schimbă direcțiile. Q/E rotesc robotul independent. Diagonalele nu depășesc viteza maximă, iar joystickul păstrează puterea analogică. Tasta N păstrează opțiunea explicită Robot-Centric; pentru direcții fixe, lasă selectat Field-Centric. Resetarea comenzilor revine la Field-Centric.

Verificările Beebot compară toate cele patru direcții la patru orientări ale robotului și trei moduri de cameră, verifică viteza diagonală și amplitudinea joystickului și confirmă că opțiunea Robot-Centric încă funcționează. Acest mod fixează direcțiile pe teren; când rotești camera, direcția W poate apărea diferit pe ecran, dar destinația pe teren rămâne aceeași.
