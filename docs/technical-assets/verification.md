# Overenie technickej príručky

Dátum: 30. 10. 2026. Preskúmaný základ: `48cb57adb57feaf1b31597f79c482388a81f0b8d` plus pracovný strom opísaný v príručke.

## Zdrojové väzby diagramov

- `scene-navigation.svg`: `StorySelectionUI.PlayArthur/PlayFather`, `SceneLoader.LoadScene`, serializované názvy v MainMenu a `FamilyMinigamePlaceholderController` s `minigameSceneName: Pipes` v Game. Šípka späť znamená obnovu stále načítanej Game a vyloženie Pipes, nie nové načítanie Game.
- `story-ownership.svg`: `StoryAccess.Current/Allows`, aktívnosť a dostupnosť vo FatherReflectionInteractable, prijatie požiadavky v DialogueUI a eventové napojenie OpeningGhostIntroController. Ide o dialógovú cestu; chladnička má namiesto dialógu vlastný UnityEvent.
- `control-locks.svg`: `PlayerControlLocker`, `StoryPlayerRig.SetSettingsOpen`, `DialogueUI.StartDialogue/EndDialogue/SetSettingsPaused` a `AudioSettingsOverlay`. Počty zobrazujú príklad bez ďalšieho cutscene zámku; ak existuje, zostáva navyše.

## Výsledky a medzery

Statická kontrola zahŕňa dôležité API, serialized referencie, Build Settings, verziu Unity, závislosti, predvolené preferencie a ich uloženie. Automatická kontrola nenašla neplatný súborový odkaz. Porovnanie textu Wordu s Markdownom po odstránení značiek formátovania potvrdilo zhodu textu skrátenej verzie pred pridaním navigácie. V balíku DOCX sú vložené tri obrázky; SVG ostávajú samostatne upraviteľné.

PNG všetkých troch diagramov boli vykreslené a vizuálne skontrolované. Celý Word sa nepodarilo vykresliť: `render_docx.py` skončil na `FileNotFoundError: LibreOffice soffice.exe was not found on PATH`. Konečné zalomenie, počet strán a vizuálna kontrola celých strán Wordu preto nie sú overené. Úspešné vytvorenie DOCX túto kontrolu nenahrádza.

Nové herné testy ani player build neboli súčasťou dokumentačnej kontroly. Historické testovacie hlásenia sú v príručke oddelené od aktuálne vykonaných statických kontrol. Zistená neistota debug busy kontroly zostáva nevyriešená.

Rozsah výstupov: technical-guide.md, tri SVG a tri PNG, tento záznam, generátor Wordu a Word mimo repozitára. SHA-256 porovnanie 2 534 existujúcich nedokumentačných súborov so vstupnou snímkou nenašlo zmenu. Súkromné poznámky zostávajú ignorované. Skripty hry, scény, nastavenia a README sa neupravovali. Nebol vykonaný commit ani push. Celorepozitárový `git diff --check` hlási pôvodné koncové medzery v rozpracovaných Unity scénach; tieto nesúvisiace zmeny zostali zachované.

## Obnovenie renderov

SVG možno exportovať do PNG v bežnom vektorovom editore; zachovaj pomer strán a aspoň 150 dpi. V tomto exporte bol použitý Sharp s `density: 150`. Potom spusti `docs/tools/generate-technical-guide.py` cez Python s python-docx. Na ďalšie vydanie treba zopakovať zdrojovú kontrolu a pri dostupnom rendereri skontrolovať všetky stránky.

Navigačná tabuľka obsahuje deväť interných odkazov. Ciele boli skontrolované v Markdown nadpisoch aj v bookmarkoch Wordu. Tabuľka má hlavičku a deväť riadkov.
