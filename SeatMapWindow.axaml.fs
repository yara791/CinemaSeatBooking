namespace CinemaSeatBooking

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform
open CinemaSeatBooking.SeatManagement
open CinemaSeatBooking.BookingLogic




type SeatMapWindow(rows: int, cols: int) as this =
    inherit Window()

    // ===== Grid Configuration ID =====
    let gridId = sprintf "%dx%d" rows cols  // e.g., "5x10" for 5 rows and 10 columns

    // ===== عناصر الواجهة الرسومية =====
    let mutable seatsPanel : StackPanel = null          // لعرض خريطة الكراسي
    let mutable numSeatsTextBox : TextBox = null       // مدخل عدد الكراسي اللي عايزة احجزهم
    let mutable generateButton : Button = null         // زرار توليد المدخلات
    let mutable seatsInputPanel : StackPanel = null    // لوحة مدخلات الصفوف والأعمدة
    let mutable reserveButton : Button = null          // زرار حجز الكراسي
    let mutable ticketsButton : Button = null          // زرار عرض التيكتات
    let mutable statusMessage : TextBlock = null       // (رسالة  (نجاح/خطأ)

    // ===== الحالة الداخلية للسينما =====
    let mutable cinemaState: CinemaState = 
        { Seats = SeatManagement.initializeSeatLayout rows cols; Tickets = [] } // initializeSeatLayout بترجع كل الكراسي كـ Available

    // ===== Dynamic seat refresh timer =====
    let mutable refreshTimer: System.Timers.Timer = null
    let mutable lastUpdateTime = DateTime.Now
    let mutable lastFileChangeTime = DateTime.MinValue
    let mutable previousSeats: Seat list = []
    let mutable recentlyChangedSeats: Set<int * int> = Set.empty
    
    // ===== File System Watcher for instant updates =====
    let mutable fileWatcher: FileSystemWatcher = null

    do
        // ===== تحميل XAML وربط عناصر الواجهة =====
        this.InitializeComponent()
        seatsPanel <- this.FindControl<StackPanel>("SeatsPanel")
        numSeatsTextBox <- this.FindControl<TextBox>("NumSeatsTextBox")
        generateButton <- this.FindControl<Button>("GenerateInputsButton")
        seatsInputPanel <- this.FindControl<StackPanel>("SeatsInputPanel")
        reserveButton <- this.FindControl<Button>("ReserveButton")
        ticketsButton <- this.FindControl<Button>("TicketsButton")
        statusMessage <- this.FindControl<TextBlock>("StatusMessageTextBlock")

        seatsPanel.HorizontalAlignment <- HorizontalAlignment.Left

        // ===== Load existing tickets from file and restore state =====
        this.LoadTicketsFromFile()
        
        // ===== Initialize previousSeats for change detection =====
        previousSeats <- cinemaState.Seats
        
        // ===== Show tickets button if there are any tickets for this grid =====
        ticketsButton.IsVisible <- cinemaState.Tickets.Length > 0

        // ===== عرض الكراسي عند فتح البرنامج =====
        this.GenerateSeats()

        // ===== Setup dynamic seat refresh timer =====
        this.SetupDynamicRefresh()

        // ===== ربط الأحداث بالزرار =====
        generateButton.Click.Add(fun _ -> this.GenerateSeatInputs() |> ignore)
        reserveButton.Click.Add(fun _ -> this.ReserveSeatsFromInputs() |> ignore)
        ticketsButton.Click.Add(fun _ -> this.ShowTicketsPage() |> ignore)
        
        // ===== Cleanup on window closing =====
        this.Closing.Add(fun _ -> this.Cleanup())

    // ===== Setup dynamic refresh timer =====
    member private this.SetupDynamicRefresh() =
        // ===== Setup periodic timer (backup mechanism) =====
        refreshTimer <- new System.Timers.Timer(5000.0) // Refresh every 5 seconds
        refreshTimer.Elapsed.Add(fun _ ->
            Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                this.RefreshSeatsDisplay()
            )
        )
        refreshTimer.Start()
        
        // ===== Setup File System Watcher for instant updates =====
        this.SetupFileWatcher()
    
    member private this.SetupFileWatcher() =
        try
            let path = "Tickets.txt"
            let directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))
            let fileName = System.IO.Path.GetFileName(path)
            
            fileWatcher <- new FileSystemWatcher(directory, fileName)
            fileWatcher.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.Size ||| NotifyFilters.CreationTime
            fileWatcher.EnableRaisingEvents <- true
            
            // Common handler for file changes
            let handleFileChange _ =
                let now = DateTime.Now
                // Only refresh if at least 200ms has passed since last file change
                if (now - lastFileChangeTime).TotalMilliseconds > 200.0 then
                    lastFileChangeTime <- now
                    // Small delay to ensure file write is complete
                    System.Threading.Thread.Sleep(150)
                    Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                        try
                            this.RefreshSeatsDisplay()
                        with ex ->
                            statusMessage.Text <- sprintf "Update error: %s" ex.Message
                    )
            
            // Handle file changes with debounce to prevent multiple rapid updates
            fileWatcher.Changed.Add(handleFileChange)
            // Also handle file creation (in case file is deleted/recreated)
            fileWatcher.Created.Add(handleFileChange)
        with ex ->
            // If file watcher fails, fallback to timer-based refresh
            statusMessage.Text <- sprintf "File watcher setup failed, using timer-based refresh: %s" ex.Message

    // ===== Refresh seats display dynamically =====
    member private this.RefreshSeatsDisplay() =
        // ===== إعادة تحميل الحالة من الملف للتزامن مع النوافذ الأخرى =====
        this.LoadTicketsFromFile()
        
        // ===== Update tickets button visibility based on loaded tickets =====
        ticketsButton.IsVisible <- cinemaState.Tickets.Length > 0
        
        // Detect seat changes
        let changes = 
            cinemaState.Seats 
            |> List.filter (fun seat ->
                match previousSeats |> List.tryFind (fun s -> s.Row = seat.Row && s.Col = seat.Col) with
                | Some prevSeat -> prevSeat.Status <> seat.Status
                | None -> false)
            |> List.map (fun s -> (s.Row, s.Col))
            |> Set.ofList
        
        if not changes.IsEmpty then
            recentlyChangedSeats <- changes
            previousSeats <- cinemaState.Seats
            this.GenerateSeats()
            // Clear highlight after 3 seconds
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(fun _ ->
                Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                    recentlyChangedSeats <- Set.empty
                    this.GenerateSeats()
                )
            ) |> ignore
        elif previousSeats.IsEmpty then
            // First refresh - initialize previousSeats
            previousSeats <- cinemaState.Seats
            this.GenerateSeats()
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(fun _ ->
                Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                    recentlyChangedSeats <- Set.empty
                    this.GenerateSeats()
                )
            ) |> ignore
        
        // Check if there were any changes (simulate checking for external updates)
        let currentTime = DateTime.Now
        if (currentTime - lastUpdateTime).TotalSeconds >= 5.0 then
            lastUpdateTime <- currentTime
    
    // ===== Cleanup resources =====
    member private this.Cleanup() =
        if refreshTimer <> null then
            refreshTimer.Stop()
            refreshTimer.Dispose()
        if fileWatcher <> null then
            fileWatcher.EnableRaisingEvents <- false
            fileWatcher.Dispose()

    // ===== تحميل XAML =====
    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

    // ===== حفظ التيكتات في الملف - دالة مساعدة =====
    member private this.SaveTicketsToFile() =
        try
            // Temporarily disable file watcher to avoid self-triggering
            if fileWatcher <> null then
                fileWatcher.EnableRaisingEvents <- false
            
            let path = "Tickets.txt"
            use writer = new StreamWriter(path)
            cinemaState.Tickets |> List.iter (fun ticket ->
                writer.WriteLine(sprintf "Grid ID: %s | Ticket ID: %A | Created At: %s" ticket.GridId ticket.Id (ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")))
                ticket.Seats |> List.iter (fun s ->
                    writer.WriteLine(sprintf "Seat: Row %d, Col %d" s.Row s.Col)
                )
                writer.WriteLine("--------------------------------------------------")
            )
            writer.Close() // Ensure file is fully written
            
            // Re-enable file watcher after a brief delay
            System.Threading.Tasks.Task.Delay(300).ContinueWith(fun _ ->
                if fileWatcher <> null then
                    fileWatcher.EnableRaisingEvents <- true
            ) |> ignore
            
            true
        with ex ->
            // Re-enable watcher even on error
            if fileWatcher <> null then
                fileWatcher.EnableRaisingEvents <- true
            statusMessage.Text <- sprintf "Error saving tickets to file: %s" ex.Message
            false

    // ===== تحميل التيكتات من الملف عند بدء التطبيق =====
    member private this.LoadTicketsFromFile() =
        try
            let path = "Tickets.txt"
            if System.IO.File.Exists(path) then
                let lines = System.IO.File.ReadAllLines(path) |> Array.toList
                let mutable reservedPositions = Set.empty<int * int>
                
                let rec parseTickets (lines: string list) (acc: Ticket list) =
                    match lines with
                    | [] -> List.rev acc
                    | line :: rest ->
                        if line.StartsWith("Grid ID:") then
                            // Parse Grid ID, Ticket ID and timestamp
                            let parts = line.Split('|')
                            let ticketGridId = parts.[0].Replace("Grid ID:", "").Trim()
                            let idPart = parts.[1].Replace("Ticket ID:", "").Trim()
                            let ticketId = Guid.Parse(idPart)
                            let datePart = parts.[2].Replace("Created At:", "").Trim()
                            let createdAt = DateTime.Parse(datePart)
                            
                            // Only load tickets that match the current grid configuration
                            if ticketGridId = gridId then
                                // Parse seats for this ticket
                                let rec parseSeats (remaining: string list) (seats: Seat list) =
                                    match remaining with
                                    | seatLine :: rest when seatLine.StartsWith("Seat:") ->
                                        // Parse "Seat: Row X, Col Y"
                                        let seatParts = seatLine.Replace("Seat:", "").Split(',')
                                        let row = seatParts.[0].Replace("Row", "").Trim() |> int
                                        let col = seatParts.[1].Replace("Col", "").Trim() |> int
                                        
                                        // Track this position as reserved
                                        reservedPositions <- reservedPositions.Add((row, col))
                                        
                                        // Create seat object with Reserved status
                                        let seat = { Row = row; Col = col; Status = SeatStatus.Reserved }
                                        parseSeats rest (seat :: seats)
                                    | _ :: rest -> (List.rev seats, rest)
                                    | [] -> (List.rev seats, [])
                                
                                let (seats, remaining) = parseSeats rest []
                                let ticket = { Id = ticketId; GridId = ticketGridId; Seats = seats; CreatedAt = createdAt }
                                parseTickets remaining (ticket :: acc)
                            else
                                // Skip tickets from different grid configurations
                                let rec skipToNextTicket (remaining: string list) =
                                    match remaining with
                                    | line :: rest when line.StartsWith("--------------------------------------------------") -> rest
                                    | _ :: rest -> skipToNextTicket rest
                                    | [] -> []
                                parseTickets (skipToNextTicket rest) acc
                        else
                            parseTickets rest acc
                
                let loadedTickets = parseTickets lines []
                
                // Update seats based on reserved positions
                // Mark seats as Reserved if in file, or Available if not
                let updatedSeats = 
                    cinemaState.Seats 
                    |> List.map (fun seat ->
                        if reservedPositions.Contains((seat.Row, seat.Col)) then
                            { seat with Status = SeatStatus.Reserved }
                        else
                            { seat with Status = SeatStatus.Available }  // Ensure freed seats are marked available
                    )
                
                // Reconstruct cinemaState with loaded data
                cinemaState <- { Seats = updatedSeats; Tickets = loadedTickets }
                statusMessage.Text <- sprintf "Loaded %d tickets for grid %s" (loadedTickets.Length) gridId
            else
                statusMessage.Text <- "No tickets file found - starting fresh"
        with ex ->
            statusMessage.Text <- sprintf "Error loading tickets from file: %s" ex.Message

    // ===== إنشاء خلية كرسي واحدة مع اللون والرمز =====
    member private this.CreateSeatCell(seat: Seat) =
        let border = new Border(Width=60.0, Height=60.0, CornerRadius=CornerRadius(5.0), Margin=Thickness(5.0))
        let text = new TextBlock(HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, FontSize=20.0)
        
        // Check if this seat recently changed
        let isRecentlyChanged = recentlyChangedSeats.Contains((seat.Row, seat.Col))
        
        match seat.Status with
        | SeatStatus.Available -> 
            border.Background <- if isRecentlyChanged then 
                                    SolidColorBrush(Color.Parse("#90EE90")) // Light green for recently freed
                                 else 
                                    SolidColorBrush(Color.Parse("#FFEB99"))
            text.Foreground <- Brushes.White
            text.Text <- "O"   // كرسي متاح
        | SeatStatus.Reserved -> 
            border.Background <- if isRecentlyChanged then
                                    SolidColorBrush(Color.Parse("#FF6B6B")) // Bright red for recently reserved
                                 else
                                    SolidColorBrush(Color.Parse("#DC143C")) // Crimson red
            text.Foreground <- Brushes.White
            text.Text <- "X"   // كرسي محجوز
        
        // Add border for recently changed seats
        if isRecentlyChanged then
            border.BorderBrush <- Brushes.Yellow
            border.BorderThickness <- Thickness(3.0)
        
        border.Child <- text
        border

    // ===== عرض كل الكراسي + الإحصائيات =====
    member private this.GenerateSeats() =
        seatsPanel.Children.Clear()
        let availableCount = getAvailableSeats cinemaState.Seats |> List.length
        let reservedCount = getReservedSeats cinemaState.Seats |> List.length

        // ===== عرض الإحصائيات مع timestamp =====
        let timestamp = DateTime.Now.ToString("HH:mm:ss")
        let stats = new TextBlock(
            Text=sprintf "Available: %d | Reserved: %d | Last Update: %s" availableCount reservedCount timestamp,
            FontSize=16.0, Foreground=Brushes.Black, Margin=Thickness(0.0,0.0,0.0,10.0))
        seatsPanel.Children.Add(stats) |> ignore

        // ===== عرض أسماء الأعمدة =====
        let headerRow = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Left)
        headerRow.Children.Add(new TextBlock(Text=" ", Width=40.0)) |> ignore
        for c in 1..cols do
            let colLabel = new TextBlock(Text=sprintf "%d" c, Width=60.0, FontSize=16.0,
                                         HorizontalAlignment=HorizontalAlignment.Center, TextAlignment=TextAlignment.Center)
            headerRow.Children.Add(colLabel) |> ignore
        seatsPanel.Children.Add(headerRow) |> ignore

        // ===== عرض كل الصفوف والكراسي =====
        for r in 1..rows do
            let rowPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Left)
            let rowLabel = new TextBlock(Text=sprintf "%d" r, Width=40.0, FontSize=16.0,
                                         HorizontalAlignment=HorizontalAlignment.Center, TextAlignment=TextAlignment.Center)
            rowPanel.Children.Add(rowLabel) |> ignore
            for c in 1..cols do
                match selectSeat r c cinemaState.Seats with
                | Some seat -> rowPanel.Children.Add(this.CreateSeatCell(seat)) |> ignore
                | None -> ()
            seatsPanel.Children.Add(rowPanel) |> ignore

    // ===== توليد مدخلات الحجز (TextBox لكل كرسي) =====
    member private this.GenerateSeatInputs() =
        seatsInputPanel.Children.Clear()
        statusMessage.Text <- ""
        let ok, count = Int32.TryParse(numSeatsTextBox.Text)
        if ok && count > 0 then
            for i in 1..count do
                let rowBox = new TextBox(Watermark=sprintf "Row %d" i, Width=60.0)
                let colBox = new TextBox(Watermark=sprintf "Col %d" i, Width=60.0)
                let panel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0)
                panel.Children.Add(rowBox) |> ignore
                panel.Children.Add(colBox) |> ignore
                seatsInputPanel.Children.Add(panel) |> ignore
        else
            statusMessage.Text <- "Enter a valid number!"

    // ===== حجز الكراسي من المدخلات =====
    member private this.ReserveSeatsFromInputs() =
        statusMessage.Text <- ""
        try
            // ===== قراءة الصفوف والأعمدة من TextBox =====
            let positions =
                seatsInputPanel.Children
                |> Seq.cast<StackPanel>
                |> Seq.choose (fun panel ->
                    try
                        let rowBox = panel.Children.[0] :?> TextBox
                        let colBox = panel.Children.[1] :?> TextBox
                        let rOk, r = Int32.TryParse(rowBox.Text)
                        let cOk, c = Int32.TryParse(colBox.Text)
                        if rOk && cOk then Some(r, c, rowBox, colBox)
                        else
                            // ===== تلوين المدخلات باللون الأحمر لو غير صالحة =====
                            rowBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                            colBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                            None
                    with _ -> None)
                |> Seq.toList

            if positions.IsEmpty then
                statusMessage.Text <- "Enter valid row and column numbers!"
            else
                let seatPositions = positions |> List.map (fun (r,c,_,_) -> (r,c))

                // ===== منع الحجز المزدوج =====
                let allAvailable =
                    seatPositions
                    |> List.forall (fun (r,c) ->
                        match selectSeat r c cinemaState.Seats with
                        | Some seat -> preventDoubleBooking seat
                        | None -> false)

                if not allAvailable then
                    statusMessage.Text <- "Some seats are already reserved!"
                else
                    // ===== إنشاء تيكت منفصل لكل كرسي =====
                    let mutable currentSeats = cinemaState.Seats
                    let mutable newTickets = []
                    let mutable allSuccessful = true
                    let mutable errorSeats = []

                    seatPositions |> List.iter (fun (r, c) ->
                        match createTicket gridId [(r, c)] currentSeats with
                        | Error badSeats ->
                            allSuccessful <- false
                            errorSeats <- (r, c) :: errorSeats
                        | Ok (ticket, updatedSeats) ->
                            currentSeats <- updatedSeats
                            newTickets <- ticket :: newTickets
                    )

                    if not allSuccessful then
                        // ===== تلوين المدخلات الغير صالحة =====
                        positions |> List.iter (fun (r,c,rowBox,colBox) ->
                            if errorSeats |> List.contains (r,c) then
                                rowBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                                colBox.Background <- SolidColorBrush(Color.Parse("#FFAAAA"))
                        )
                        statusMessage.Text <- "Some seats are invalid or already reserved!"
                    else
                        // ===== تحديث الحالة الداخلية للسينما =====
                        previousSeats <- cinemaState.Seats // Save previous state
                        cinemaState <- { cinemaState with Seats = currentSeats; Tickets = newTickets @ cinemaState.Tickets }
                        
                        // Mark changed seats for highlighting
                        recentlyChangedSeats <- seatPositions |> Set.ofList
                        
                        // ===== كتابة التيكتات مباشرة في الملف بعد الحجز =====
                        if this.SaveTicketsToFile() then
                            // ===== إعادة تحميل الحالة من الملف للتزامن مع النوافذ الأخرى =====
                            this.LoadTicketsFromFile()
                            this.GenerateSeats()
                            statusMessage.Text <- sprintf "Reservation successful! %d ticket(s) created and saved." newTickets.Length
                        else
                            this.GenerateSeats()
                            statusMessage.Text <- sprintf "Reservation successful! %d ticket(s) created but file save failed." newTickets.Length
                        
                        ticketsButton.IsVisible <- true
                        
                        // Clear highlight after 3 seconds
                        System.Threading.Tasks.Task.Delay(3000).ContinueWith(fun _ ->
                            Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                                recentlyChangedSeats <- Set.empty
                                this.GenerateSeats()
                            )
                        ) |> ignore
        with ex ->
            statusMessage.Text <- sprintf "Error: %s" ex.Message

    // ===== صفحة عرض التيكتات مع أزرار Download & Back =====
    member private this.ShowTicketsPage() =
        let ticketsWindow = new Window(Title="Tickets", Width=900.0, Height=600.0)
        let assets = AssetLoader.Open(new Uri("avares://CinemaSeatBooking/images/background_tickets.png"))
        let imgBrush = new ImageBrush(Source=new Bitmap(assets), Stretch=Stretch.UniformToFill)
        ticketsWindow.Background <- imgBrush

        let scroll = new ScrollViewer(Margin=Thickness(20.0))
        let panel = new StackPanel(Spacing=10.0)

        let title = new TextBlock(Text="Your Tickets", FontSize=28.0, Foreground=Brushes.Red,
                                  HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.0,0.0,0.0,20.0))
        panel.Children.Add(title) |> ignore

        // ===== زر Back فقط =====
        let buttonsPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Center)
        let backButton = new Button(Content="Back", Width=100.0, Background=SolidColorBrush(Color.Parse("#FF5555")), Foreground=Brushes.White)
        buttonsPanel.Children.Add(backButton) |> ignore
        panel.Children.Add(buttonsPanel) |> ignore

        // ===== زر العودة =====
        backButton.Click.Add(fun _ -> ticketsWindow.Close())

        // ===== عرض كل التيكتات =====
        if cinemaState.Tickets.Length > 0 then
            cinemaState.Tickets |> List.iter (fun ticket ->
                let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")), CornerRadius=CornerRadius(5.0),
                                        Padding=Thickness(10.0), Margin=Thickness(5.0))
                let stack = new StackPanel(Spacing=5.0)

                // ===== تفاصيل التيكت =====
                stack.Children.Add(
                    new TextBlock(
                        Text=sprintf "🎫 Ticket ID: %A | Created At: %s"
                            ticket.Id (ticket.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                        FontSize=16.0, Foreground=Brushes.Red)) |> ignore

                ticket.Seats |> List.iter (fun s ->
                    stack.Children.Add(new TextBlock(Text=sprintf "Seat: Row %d, Col %d" s.Row s.Col,
                                                     FontSize=16.0, Foreground=Brushes.Red)) |> ignore)

                // ===== زر إلغاء التيكت =====
                let cancelButton = new Button(Content="Cancel Ticket", Width=120.0,
                                              Background=SolidColorBrush(Color.Parse("#FF5555")),
                                              Foreground=Brushes.White)

                cancelButton.Click.Add(fun _ ->
                    match cancelReservation ticket.Id cinemaState with
                    | Error msg -> statusMessage.Text <- msg
                    | Ok updatedState ->
                        // Mark cancelled seats for highlighting
                        let cancelledSeats = ticket.Seats |> List.map (fun s -> (s.Row, s.Col)) |> Set.ofList
                        recentlyChangedSeats <- cancelledSeats
                        
                        previousSeats <- cinemaState.Seats
                        cinemaState <- updatedState
                        
                        // Update Tickets.txt file after deletion
                        if this.SaveTicketsToFile() then
                            // ===== إعادة تحميل الحالة من الملف للتأكد من التزامن =====
                            this.LoadTicketsFromFile()
                            statusMessage.Text <- "Ticket cancelled, file updated, and grid refreshed"
                        else
                            statusMessage.Text <- "Ticket cancelled but error updating file"
                        
                        // ===== Update tickets button visibility =====
                        ticketsButton.IsVisible <- cinemaState.Tickets.Length > 0
                        
                        // Regenerate the grid with updated seats
                        this.GenerateSeats()
                        
                        // Clear highlight after 3 seconds
                        System.Threading.Tasks.Task.Delay(3000).ContinueWith(fun _ ->
                            Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                                recentlyChangedSeats <- Set.empty
                                this.GenerateSeats()
                            )
                        ) |> ignore
                        
                        // Refresh the current window content without opening a new one
                        panel.Children.Clear()
                        
                        let title = new TextBlock(Text="Your Tickets", FontSize=28.0, Foreground=Brushes.Red,
                                                  HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.0,0.0,0.0,20.0))
                        panel.Children.Add(title) |> ignore

                        let buttonsPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Center)
                        let backButton = new Button(Content="Back", Width=100.0, Background=SolidColorBrush(Color.Parse("#FF5555")), Foreground=Brushes.White)
                        buttonsPanel.Children.Add(backButton) |> ignore
                        panel.Children.Add(buttonsPanel) |> ignore
                        backButton.Click.Add(fun _ -> ticketsWindow.Close())

                        if cinemaState.Tickets.Length > 0 then
                            cinemaState.Tickets |> List.iter (fun t ->
                                let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")), CornerRadius=CornerRadius(5.0),
                                                        Padding=Thickness(10.0), Margin=Thickness(5.0))
                                let stack = new StackPanel(Spacing=5.0)
                                stack.Children.Add(
                                    new TextBlock(
                                        Text=sprintf "🎫 Ticket ID: %A | Created At: %s"
                                            t.Id (t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                                        FontSize=16.0, Foreground=Brushes.Red)) |> ignore
                                t.Seats |> List.iter (fun s ->
                                    stack.Children.Add(new TextBlock(Text=sprintf "Seat: Row %d, Col %d" s.Row s.Col,
                                                                     FontSize=16.0, Foreground=Brushes.Red)) |> ignore)
                                let cancelBtn = new Button(Content="Cancel Ticket", Width=120.0,
                                                          Background=SolidColorBrush(Color.Parse("#FF5555")),
                                                          Foreground=Brushes.White)
                                cancelBtn.Click.Add(fun _ ->
                                    match cancelReservation t.Id cinemaState with
                                    | Error msg -> statusMessage.Text <- msg
                                    | Ok updatedState ->
                                        cinemaState <- updatedState
                                        
                                        // Update Tickets.txt file after deletion
                                        if this.SaveTicketsToFile() then
                                            // ===== إعادة تحميل الحالة من الملف للتأكد من التزامن =====
                                            this.LoadTicketsFromFile()
                                            statusMessage.Text <- "Ticket cancelled, file updated, and grid refreshed"
                                        else
                                            statusMessage.Text <- "Ticket cancelled but error updating file"
                                        
                                        // ===== Update tickets button visibility =====
                                        ticketsButton.IsVisible <- cinemaState.Tickets.Length > 0
                                        
                                        // Regenerate the grid with updated seats
                                        this.GenerateSeats()
                                        
                                        panel.Children.Clear()
                                        let title = new TextBlock(Text="Your Tickets", FontSize=28.0, Foreground=Brushes.Red,
                                                                  HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.0,0.0,0.0,20.0))
                                        panel.Children.Add(title) |> ignore
                                        let buttonsPanel = new StackPanel(Orientation=Orientation.Horizontal, Spacing=10.0, HorizontalAlignment=HorizontalAlignment.Center)
                                        let backButton = new Button(Content="Back", Width=100.0, Background=SolidColorBrush(Color.Parse("#FF5555")), Foreground=Brushes.White)
                                        buttonsPanel.Children.Add(backButton) |> ignore
                                        panel.Children.Add(buttonsPanel) |> ignore
                                        backButton.Click.Add(fun _ -> ticketsWindow.Close())
                                        if cinemaState.Tickets.Length = 0 then
                                            let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")),
                                                                    CornerRadius=CornerRadius(5.0), Padding=Thickness(10.0),
                                                                    Margin=Thickness(5.0))
                                            let textBlock = new TextBlock(Text="No tickets booked yet!", FontSize=16.0, Foreground=Brushes.Red)
                                            border.Child <- textBlock
                                            panel.Children.Add(border) |> ignore
                                )
                                stack.Children.Add(cancelBtn) |> ignore
                                border.Child <- stack
                                panel.Children.Add(border) |> ignore)
                        else
                            let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")),
                                                    CornerRadius=CornerRadius(5.0), Padding=Thickness(10.0),
                                                    Margin=Thickness(5.0))
                            let textBlock = new TextBlock(Text="No tickets booked yet!", FontSize=16.0, Foreground=Brushes.Red)
                            border.Child <- textBlock
                            panel.Children.Add(border) |> ignore
                )

                stack.Children.Add(cancelButton) |> ignore
                border.Child <- stack
                panel.Children.Add(border) |> ignore)
        else
            // ===== حالة عدم وجود تيكتات =====
            let border = new Border(Background=SolidColorBrush(Color.Parse("#88FFFFFF")),
                                    CornerRadius=CornerRadius(5.0), Padding=Thickness(10.0),
                                    Margin=Thickness(5.0))
            let textBlock = new TextBlock(Text="No tickets booked yet!", FontSize=16.0, Foreground=Brushes.Red)
            border.Child <- textBlock
            panel.Children.Add(border) |> ignore

        scroll.Content <- panel
        ticketsWindow.Content <- scroll
        ticketsWindow.Show() |> ignore
