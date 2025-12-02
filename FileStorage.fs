namespace CinemaSeatBooking

module BookingLogic =

    open CinemaSeatBooking
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization

    // ≈⁄œ«œ«  JSON ··Õ›Ÿ Ê«· Õ„Ì·
    let private jsonOptions = 
        let options = JsonSerializerOptions()
        options.WriteIndented <- true
        options.Converters.Add(JsonFSharpConverter())
        options

    // Õ›Ÿ «·»Ì«‰«  ›Ì „·› JSON
    let saveToFile (filePath: string) (state: CinemaState) =
        try
            let json = JsonSerializer.Serialize(state, jsonOptions)
            File.WriteAllText(filePath, json)
            Ok "Data saved successfully"
        with
        | ex -> Error $"Failed to save file: {ex.Message}"

    //  Õ„Ì· «·»Ì«‰«  „‰ „·› JSON
    let loadFromFile (filePath: string) =
        try
            if File.Exists(filePath) then
                let json = File.ReadAllText(filePath)
                let state = JsonSerializer.Deserialize<CinemaState>(json, jsonOptions)
                Ok state
            else
                Error "File not found"
        with
        | ex -> Error $"Failed to load file: {ex.Message}"

    // Õ–› „·› «·»Ì«‰« 
    let deleteFile (filePath: string) =
        try
            if File.Exists(filePath) then
                File.Delete(filePath)
                Ok "File deleted successfully"
            else
                Error "File not found"
        with
        | ex -> Error $"Failed to delete file: {ex.Message}"

    // «· Õﬁﬁ „‰ ÊÃÊœ «·„·›
    let fileExists (filePath: string) =
        File.Exists(filePath)

    // Õ›Ÿ ‰”Œ… «Õ Ì«ÿÌ…
    let createBackup (filePath: string) =
        try
            if File.Exists(filePath) then
                let backupPath = $"{filePath}.backup"
                File.Copy(filePath, backupPath, true)
                Ok $"Backup created: {backupPath}"
            else
                Error "Original file not found"
        with
        | ex -> Error $"Failed to create backup: {ex.Message}"
