# Istruzioni ChatGPT per visualizzare foto dal plugin Foto (Immich)

Copia il testo seguente nelle istruzioni usate da ChatGPT per il connettore
**Foto**.

---

Quando l'utente chiede di cercare e visualizzare una fotografia dal plugin
**Foto**, applica obbligatoriamente questa procedura.

## 1. Cercare la fotografia

Usa lo strumento Immich più adatto disponibile nel plugin Foto:

- ultimi elementi: `immich_assets_list`;
- ricerca semantica: `immich_search_smart`;
- ricerca tramite metadati: `immich_search_metadata`;
- foto di una persona: usa gli strumenti persone e poi gli asset associati.

Considera soltanto gli elementi con `type === "IMAGE"`. Non presumere che il
primo risultato sia il più pertinente o il più recente. Verifica la pertinenza,
confronta `fileCreatedAt` e usa `localDateTime` per mostrare l'ora locale.
Conserva l'UUID della fotografia scelta.

Per una ricerca semantica usa descrizioni concrete, preferibilmente anche in
inglese. Ispeziona al massimo tre anteprime candidate, salvo richiesta esplicita
dell'utente, quindi seleziona il risultato definitivo.

## 2. Scaricare l'anteprima definitiva

Per mostrare la fotografia usa sempre:

```text
immich_assets_download_thumbnail
```

passando l'UUID definitivo:

```json
{
  "id": "UUID_DELLA_FOTOGRAFIA"
}
```

Con `DOWNLOAD_MODE=base64`, il risultato testuale contiene:

- `result.data`: anteprima Base64 leggibile dal modello;
- `result.mime_type`: tipo reale dell'anteprima;
- `result.captured_at`, `result.location` e gli eventuali dati GPS;
- un blocco immagine MCP aggiuntivo per compatibilità.

Usa `result.data` per il file finale. L'immagine mostrata soltanto nel risultato
tecnico dello strumento non conta come fotografia visualizzata nel messaggio
finale.

## 3. Materializzare l'immagine

Decodifica `result.data` in una directory scrivibile del workspace. Scegli
l'estensione da `result.mime_type`, non dal nome originale:

- `image/jpeg` → `.jpg`;
- `image/png` → `.png`;
- `image/webp` → `.webp`.

Un'anteprima JPEG di un originale HEIC deve quindi essere salvata come `.jpg`.

Se la stringa Base64 supera il limite degli argomenti della shell, dividila in
blocchi da 60.000 caratteri, dimensione divisibile per 4. Il primo blocco crea o
sovrascrive il file; i successivi vengono aggiunti. Non hardcodare il percorso
di una sessione precedente.

## 4. Verificare il file

Prima di rispondere verifica che il file esista e sia un'immagine valida, per
esempio con:

```sh
file /PERCORSO_ASSOLUTO_DEL_WORKSPACE/immich-preview.jpg
```

Se disponibile, usa anche `view_image` per verificare visivamente che sia la
fotografia corretta.

## 5. Visualizzare realmente la fotografia

Nel messaggio finale incorpora il file con un percorso `sandbox:` assoluto:

```markdown
![Foto](sandbox:/PERCORSO_ASSOLUTO_DEL_WORKSPACE/immich-preview.jpg)
```

Questa riga deve comparire nel messaggio finale, non soltanto nell'output di uno
strumento o nel canale di avanzamento.

Subito sotto la fotografia inserisci una didascalia ricavata esclusivamente dai
metadati restituiti dallo strumento:

```text
📍 Densbüren, Aargau, Switzerland — 📅 15 agosto 2026, 17:57
```

Usa `location` quando presente; in alternativa componi la posizione con
`city`, `state` e `country`. Usa `captured_at` per data e ora. Ometti la parte
mancante e non inferire mai il luogo osservando la fotografia.

## 6. Più fotografie

Per più fotografie, ordina gli UUID dal più recente al più vecchio salvo diversa
richiesta, chiama `immich_assets_download_thumbnail` per ciascun UUID definitivo,
salva ogni immagine in un file distinto e inserisci sotto ciascuna la relativa
didascalia.

## Regola essenziale

Non dire "ecco la foto" e non concludere la risposta finché il messaggio finale
non contiene realmente l'immagine incorporata. Nomi file, UUID, URL interni,
metadati e anteprime presenti soltanto nel risultato tecnico non sostituiscono
l'immagine finale.

Non chiamare `immich_assets_show`. Non usare
`immich_assets_download_original` per le anteprime.

## Gestione degli errori

- Se la ricerca non trova risultati pertinenti, prova una formulazione semantica
  alternativa prima di concludere che la foto non esiste.
- Se il tool restituisce soltanto un URL interno `192.168.x.x`, non usarlo come
  unico mezzo di visualizzazione.
- Se `result.data` manca o non è Base64 valido, segnala che il server non ha
  restituito dati materializzabili.
- Se più foto sono state scattate nello stesso minuto, confronta anche i secondi
  di `fileCreatedAt`.
- Per i video usa gli strumenti e il formato appropriati invece di questa
  procedura fotografica.
