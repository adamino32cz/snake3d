# Snake 3D (Unity 2022.3 LTS)

## ▶ Hrát v prohlížeči: https://adamino32cz.github.io/snake3d/

Funguje v Chrome, Edge a Firefoxu na počítači (Safari na Macu by mělo také). Načtení poprvé trvá chvíli (stahuje se cca desítky MB), potom se hra ukládá do mezipaměti prohlížeče. Ovládání: šipky / WASD, pauza **P** (Esc v prohlížeči jen ukončí celou obrazovku).

Kód projektu: https://github.com/adamino32cz/snake3d – otevři složku v Unity Hubu (Unity 2022.3.62f3).

**Nová verze na web:** `Tools/publish_webgl.sh` (sestaví WebGL a nahraje ho do větve `gh-pages`).

## Návrh hry

**Mřížka vs. volný pohyb – zvolena mřížka.** Had se pohybuje po mřížce 20×20 na ploché 3D aréně, každých 0,16 s (v dalších levelech rychleji) o jedno políčko. Důvody: logika je jednoduchá a čitelná (pozice = celá čísla `Vector2Int`), kolize se řeší porovnáním buněk bez fyziky a Rigidbody, článek těla prostě zabere předchozí buňku článku před ním, a chování je předvídatelné a snadno se testuje – ideální pro školní projekt. Volný pohyb (zatáčení o úhel) by vypadal plynuleji, ale vyžaduje ukládání historie pozic, fyzikální kolize a řešení „samo-kolize“ u hlavy, což je zbytečně složité.

**Překážky.** Točící se kotoučové pily (3D model z Kenney Platformer Kit), které jezdí tam a zpět mezi dvěma waypointy (`_WP_A` / `_WP_B`). Pod každou svítí červená varovná skvrna. Dlouhá překážka má dvě pily. Had zemře, když vjede do buňky, kterou pila právě zabírá (kolize se počítá podle neviditelné kostky, ne podle modelu).

**Typy jídla** (3D modely z balíčků Kenney, každé svítí svou barvou a má pod sebou svítící auru):

| Jídlo | 3D model | Body | Efekt | Četnost |
|---|---|---|---|---|
| Jablko | jablko | +10 | had +1 článek | častá |
| Zlaté | zlatá hvězda | +30 | had +2 články | vzácná |
| Rychlost (turbo) | plechovka (energiťák) | +15 | +1 článek, 5 s zrychlení, svítící stopa | střední |
| Čas | šálek kávy | +5 | +10 s k časovému limitu | střední |
| Zmenšení | houba | +5 | had −2 články (min. 2) | vzácná |
| Štít | srdce | +10 | 6 s nesmrtelnost vůči překážkám a vlastnímu tělu (had hraje duhou), do zdi narazíš i tak | vzácná |
| Magnet | červený magnet (poskládaný z kostek v kódu) | +10 | 8 s přitahuje jídlo do vzdálenosti 7 políček k hlavě | vzácná |

Na hrací ploše jsou vždy 3 jídla; snědené se ihned nahradí novým. Svítící šipka nad hlavou hada ukazuje k nejbližšímu jídlu. Vpravo dole je **minimapa** (druhá kamera kouká shora do `RenderTexture`), pod časem se ukazují aktivní vylepšení (TURBO / ŠTÍT / MAGNET) a kolik sekund jim zbývá.

**Combo.** Když sníš další jídlo do 2,5 s po předchozím, combo roste: od 3 jídel za sebou jsou body ×2, od 6 ×3. Tón zvuku přitom stoupá.

**Kampaň s úkoly (10 levelů).** Hra je kampaň: každý level má svůj **úkol (cíl)**. Když ho splníš, ukáže se „LEVEL N SPLNĚN!“, odemkne se další level (uloží se do `PlayerPrefs`, klíč `Snake3D_Unlocked`) a pokračuješ dál. Na úvodní obrazovce jde vybrat jen odemčené levely (ostatní mají nápis „zámek“). Aktuální úkol a postup vidíš vlevo nahoře (např. „Cíl: Sněz 10 jídel 4/10“), na začátku levelu se ukáže karta s úkolem.

| Level | Název | Úkol | Co je nového | Had (krok) |
|---|---|---|---|---|
| 1 | Neonové město | sněz 10 jídel | 1 stojící pila, bez časového limitu | 0,17 s |
| 2 | Dvě pily | sněz 15 jídel | 3 stojící pily | 0,16 s |
| 3 | První pohyb | sněz 15 jídel | první pomalu jezdící pila | 0,155 s |
| 4 | Zlatá hvězda | sněz 2× zlatou hvězdu | úkol na určitý druh jídla, 2 jezdící pily | 0,15 s |
| 5 | Dlouhý had | dorůst na 15 článků | dlouhá pila (dvě za sebou), pozor na houby | 0,145 s |
| 6 | Bez turba | sněz 12 jídel bez turba | snězení turba = konec | 0,14 s |
| 7 | Combo mistr | udělej COMBO x3 | 4 pily | 0,135 s |
| 8 | Temnota | přežij 45 s | tma, svítí jen lampa nad hadem | 0,135 s |
| 9 | Obří pila | sněz 20 jídel do 75 s | BOSS – obří pila přes 3 políčka, časový limit | 0,13 s |
| 10 | FINÁLE | sněz 25 jídel do 90 s | 8 pil včetně obří | 0,115 s |

Vše je v datech: `LevelData.cs` (enum `Objective` = druh úkolu: `EatFood`, `EatKind`, `ReachLength`, `Combo`, `Survive`; `target` = kolik; `foodKind`; `noTurbo`; `timeLimit`; `dark`; pily `obstacles`). Levely se zapisují na jeden řádek v `SnakeSceneSetup.cs` pomocí `Lvl(...)`, `Goal(...)` a `Route(...)` (stejný začátek a konec + rychlost 0 = stojící pila, poslední číslo = velikost pily). V Unity je jde upravit i v Inspectoru na objektu **GameManager → Levels**.

**Časový limit.** Jen v levelech, které ho mají v úkolu (9 a 10); jinde je vpravo nahoře „ČAS --“. Káva přidává čas, posledních 10 s čas červeně bliká.

**Skórování.** Body podle tabulky výše (krát combo). Rekord a tabulka **TOP 10** se ukládají přes `PlayerPrefs` (soubor `HighScores.cs`). Když se dostaneš do TOP 10, napíšeš po konci hry své jméno (Enter = uložit).

**Ovládání.**
- Úvodní obrazovka: **1–9, 0** nebo šipky vlevo/vpravo = výběr odemčeného levelu, **mezerník** = start.
- Hra: **W/A/S/D** nebo šipky (otočka o 180° není možná), **Esc** nebo **P** = pauza.
- Pauza: šipky vlevo/vpravo = hlasitost hudby, nahoru/dolů = hlasitost zvuků (uloží se), **R** = znovu, **Q** = úvodní obrazovka.
- Konec hry: napiš jméno + Enter (když jsi v TOP 10), pak **R** / mezerník / tlačítko „Hrát znovu“.

## Struktura projektu

```
snake3d/
├── Assets/
│   ├── Scripts/   GameManager, SnakeController, FoodSpawner, Food, FoodType, MovingObstacle, GridArena,
│   │              NeonFX, NeonPulse, CameraRig, UIPunch, SoundFX, FloatingText, SnakeAutoplay (demo),
│   │              LevelData (kampaň + úkoly), HighScores, FoodArrow, Spin, Tongue
│   ├── Models/    3D modely Kenney (KenneyFoodKit, KenneyPlatformerKit) + License.txt
│   ├── Audio/     eat.wav, turbo.wav, crash.wav, music.wav (vyrobené programem)
│   ├── Editor/    SnakeSceneSetup.cs (automatická stavba scény), SnakeBatch.cs
│   ├── Settings/  URP nastavení + profil post-processingu
│   ├── Textures/  NeonGrid.png, SoftDot.png (vyrobené kódem)
│   ├── Prefabs/   (vygeneruje se: Food_*.prefab)
│   ├── Materials/ (vygeneruje se)
│   └── Scenes/    (vygeneruje se: Snake3D.unity)
├── Tools/generate_audio.py       (Python skript, který vyrobí zvuky)
├── Packages/manifest.json        (URP 14.0.12, TextMeshPro 3.0.6, uGUI)
└── ProjectSettings/ProjectVersion.txt (2022.3.62f3)
```

## Spuštění A – automaticky (doporučeno)

1. V Unity Hub: **Add → Add project from disk** → vyber složku `snake3d`. (Pokud máš jinou verzi 2022.3.x, Hub nabídne její použití – potvrď.)
2. Po otevření se editor skript sám spustí: naimportuje TMP Essential Resources, vytvoří materiály, prefaby jídla a scénu `Assets/Scenes/Snake3D.unity` a přidá ji do Build Settings.
3. Otevři `Assets/Scenes/Snake3D.unity` (pokud už není otevřená) a stiskni **Play**.
4. Kdykoli lze scénu postavit znovu: **Tools → Snake 3D → Vytvořit scénu**.

Pokud by texty nebyly vidět: *Window → TextMeshPro → Import TMP Essential Resources* a znovu spusť menu.

Alternativa: vytvoř nový projekt ze šablony **3D (URP)** a zkopíruj do něj složky `Assets/Scripts`, `Assets/Editor`, `Assets/Models` a `Assets/Audio`, pak menu Tools → Snake 3D → Vytvořit scénu. Skript si URP nastaví sám (balíček `com.unity.render-pipelines.universal` musí být v projektu).

## Spuštění B – ručně

1. Nová scéna. Vytvoř prázdný objekt **Arena** + komponenta `GridArena` (20×20, cellSize 1).
2. Podlaha: Cube, pozice (0,−0.1,0), scale (20,0.2,20), zelený materiál. 4 zdi: Cube scale (22,1,1) na z=±10.5 a (1,1,20) na x=±10.5.
3. **SnakeHead**: Cube scale 0.95, odstraň Collider, přidej `SnakeController`, nastav Arena, Body Material.
4. Jídla: pro každý typ vytvoř primitiv (scale 0.6, bez collideru), barevný materiál, komponentu `Food` a vyplň `Type` (kind, name, color, points, grow, bonusTime, spawnWeight dle tabulky). Přetáhni do `Assets/Prefabs`, ze scény smaž.
5. **FoodSpawner**: prázdný objekt + `FoodSpawner`; přiřaď Arena, Snake, GameManager, Food Prefabs (všech 5).
6. Překážky: Cube (oranžový) + `MovingObstacle`; vytvoř dva prázdné objekty jako waypointy a přetáhni je do pole Waypoints; nastav Speed.
7. **GameManager**: prázdný objekt + `GameManager`; přiřaď Arena, Snake, Spawner, Obstacles (všechny), UI prvky. Ve SnakeControlleru přiřaď GameManager.
8. Kamera: pozice (0,22,−16), rotace (55,0,0).
9. UI: Canvas (Scale With Screen Size 1920×1080) → TMP texty ScoreText (vlevo nahoře), TimeText (vpravo nahoře), InfoText (nahoře uprostřed); **GameOverPanel** (Image, černá 75 %) s TMP textem GameOverText a tlačítkem RestartButton → OnClick: GameManager.RestartGame. Panel deaktivuj.
10. Ulož scénu do `Assets/Scenes`, přidej ji do *File → Build Settings* a stiskni Play.

## Demo režim (nepovinné)
Skript `Assets/Scripts/SnakeAutoplay.cs` umí hrát sám a fotit screenshoty. Při normální hře se nespustí.
Zapne se jen parametrem při spuštění sestavené hry, např. na Linuxu:
`./Snake3D.x86_64 -autoplay -screenshots -shotdir ./Screenshots`
Další parametry pro testování: `-resetcampaign` (zamkne zase všechny levely), `-tutorial` (ukáže nápovědu znovu), `-startlevel 3` (začne rovnou ve 3. levelu), `-onlyfood Magnet` (na ploše bude jen jeden druh jídla – hodí se na vyzkoušení).
Pomocné příkazy pro příkazovou řádku jsou v `Assets/Editor/SnakeBatch.cs` (import TMP, stavba scény, build pro Linux).

## Grafika (neonový styl)

Hra používá **URP** (Universal Render Pipeline) – moderní způsob kreslení v Unity, který běží na Windows, Linuxu (Ubuntu) i Macu. Nepotřebuje ray tracing ani silnou grafickou kartu.
Všechno nastaví skript `Assets/Editor/SnakeSceneSetup.cs` (menu **Tools → Snake 3D → Vytvořit scénu**, samotné URP: **Tools → Snake 3D → Nastavit URP grafiku**).

- **URP + HDR**: nastavení je v `Assets/Settings/Snake_URP.asset`. HDR znamená, že barva může být „jasnější než bílá“ – díky tomu pak věci září.
- **Emission (záře materiálu)**: každý materiál (`Assets/Materials`) má tmavou základní barvu a k tomu *emission* = barvu, kterou objekt sám svítí. Číslo síly (např. ×3) říká, jak moc svítí.
- **Bloom**: efekt post-processingu – kolem jasných (svítících) věcí se vytvoří měkká záře. Nastavuje se v objektu **PostProcessVolume** (profil `Assets/Settings/NeonVolumeProfile.asset`). Dál je tam *Tonemapping ACES* (filmové barvy), *Vignette* (ztmavené okraje), *Color Adjustments* (kontrast, sytost) a jemná *Chromatic Aberration*.
- **Mřížka podlahy**: textura `Assets/Textures/NeonGrid.png` je vyrobená kódem (jedno políčko se svítícím okrajem) a opakuje se 20×20.
- **Mlha a tmavé pozadí**: v nastavení osvětlení (Lighting) – okolní podlaha se do dálky ztrácí.
- **Částice (Particle System)**: `NeonFX.cs` vypustí hromádku svítících teček, když had sní jídlo (v barvě jídla), při turbu a při nárazu.
- **Plynulý pohyb**: logika hada je pořád na mřížce (`SnakeController.cs`), ale kuličky těla se mezi políčky posouvají plynule (funkce `Vector3.Lerp`). Hlava se plynule natáčí. Tělo má barevný přechod od hlavy k ocasu a ocas se zužuje.
- **Turbo stopa**: při zrychlení táhne hlava za sebou svítící žlutooranžovou stopu (*TrailRenderer* s průhledným „sčítacím“ materiálem `NeonTrail`, jehož barva je jasnější než bílá, takže září) a odletují z ní jiskry.
- **Plovoucí body**: po snězení vyletí nad jídlem text „+10“ (u času i „+10s“), stoupá a mizí (`FloatingText.cs`).
- **Had**: hlava z primitiv – oči se zorničkami, naštvané obočí, nozdry a rozeklaný jazyk, který občas vystrčí (`Tongue.cs`). Tělo má šupiny (textura `SnakeScales.png` vyrobená kódem), zužuje se k ocasu a jemně se vlní do stran (sinus v `SnakeController`). Volný CC0 model hadí hlavy jsme v balíčcích Kenney nenašli, proto je hlava poskládaná v kódu.
- **Další efekty**: nápověda při první hře, oslava „NOVÝ REKORD!“, aktivní vylepšení pod časem (vlastní materiál písma bez záře, ať jsou barvy syté). Na modelech Kenney jsou vypnuté stíny a textura má filtr Point – tím zmizely tmavé tečky na pile a plechovce.
- **3D modely**: jídlo a pily jsou modely Kenney (nízkopolygonové, s jednou barevnou texturou `colormap.png`). Materiál `KenneyMat` jim přidá záři, takže pasují do neonu. Pily se otáčejí (`Spin.cs`).
- **Levely a barvy**: barvu mřížky a okrajů mění `GameManager.ApplyLevel` pomocí *MaterialPropertyBlock* (změní barvu jen danému objektu, materiál zůstane stejný). Mřížka navíc pulzuje do rytmu hudby a kolem arény stoupá svítící „prach“ (částice).
- **„Šťáva“ (game feel)**: při nárazu had vybuchne článek po článku, jídlo se objeví s „vyskočením“, kamera na úvodní obrazovce krouží kolem arény; jídlo se točí a poskakuje (`Food.cs`), překážky pulzují (`NeonPulse.cs`), kamera lehce sleduje hada a při nárazu se zatřese (`CameraRig.cs`), skóre při změně poskočí (`UIPunch.cs`).
- **UI**: texty mají neonovou záři a obrys (materiály `NeonText_Cyan` a `NeonText_Magenta`), úvodní obrazovka „3D SNAKE – stiskni MEZERNÍK“ a nový panel Konec hry.

Tip na úpravy: barvy hada jsou v Inspectoru na objektu **SnakeHead** (Head Color, Tail Color, Glow), síla záře celé scény v **PostProcessVolume → Bloom → Intensity**.

## Zvuky a hudba

Všechny zvuky jsou **vyrobené programem**, takže se nic nestahuje a nejsou s nimi problémy s licencí. Skript `Tools/generate_audio.py` (stačí Python 3: `python3 Tools/generate_audio.py`) spočítá zvukové vlny (sinus, obdélník, pila, šum) a uloží je do `Assets/Audio`:
- `eat.wav` – krátké „blip“ při snězení (zlaté jídlo má vyšší tón, zmenšující nižší),
- `turbo.wav` – „vžum“ při zrychlení,
- `crash.wav` – hluboké „bum“ při nárazu (potom hudba ztichne),
- `music.wav` – synthwave smyčka (basa, arpeggio, buben), která hraje dokola.

Přehrává je skript `SoundFX.cs` na objektu **SoundFX**. V Inspectoru tam nastavíš **Effects Volume** (zvuky) a **Music Volume** (hudba), obojí 0 až 1. Vlastní zvuk vyměníš tak, že přetáhneš jiný AudioClip do příslušného políčka.

## Zdroje a licence

Všechny 3D modely jsou zdarma s licencí **CC0** (public domain) – smí se použít i ve školním projektu bez omezení, uvedení autora není povinné, ale je slušné:

| Balíček | Autor | Odkaz | Licence | Co používáme |
|---|---|---|---|---|
| Food Kit (2.0) | Kenney (www.kenney.nl) | https://kenney.nl/assets/food-kit | CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/) | jablko, houba, plechovka (soda-can), šálek kávy (cup-coffee) |
| Platformer Kit (4.1) | Kenney (www.kenney.nl) | https://kenney.nl/assets/platformer-kit | CC0 1.0 | hvězda (star), srdce (heart), kotoučová pila (saw) |

Licenční soubory jsou přiložené v `Assets/Models/KenneyFoodKit/License.txt` a `Assets/Models/KenneyPlatformerKit/License.txt`.
Zvuky a hudbu jsme vyrobili sami programem (`Tools/generate_audio.py`), písmo LiberationSans je součástí TextMesh Pro (licence SIL OFL).

**Tip:** modely Kenney mají plochy s jedinou UV souřadnicí. Proto mají v importu vypnuté **tangenty** (jinak vznikají chybné pixely „NaN“, které bloom roztáhne do bílé skvrny) – nastavuje to `SnakeSceneSetup` automaticky.
