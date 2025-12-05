namespace CinemaSeatBooking

module SeatManagement =

    open System.Threading
    open CinemaSeatBooking

    // Thread-safe lock object for preventing concurrent booking conflicts
    let private bookingLock = obj()

    // Initialize الكراسي
    let initializeSeatLayout rows cols =
        [ for r in 1..rows do
            for c in 1..cols do
                yield { Row = r; Col = c; Status = SeatStatus.Available } ]

    // اختار كرسي معين
    let selectSeat row col seats =
        seats |> List.tryFind (fun s -> s.Row = row && s.Col = col)

    // منع الحجز المكرر - Thread-safe version
    let preventDoubleBooking seat =
        lock bookingLock (fun () ->
            seat.Status <> SeatStatus.Reserved
        )

    // عرض الكراسي المتاحة - Thread-safe version
    let getAvailableSeats seats =
        lock bookingLock (fun () ->
            seats |> List.filter (fun s -> s.Status = SeatStatus.Available)
        )

    // عرض الكراسي المحجوزة - Thread-safe version
    let getReservedSeats seats =
        lock bookingLock (fun () ->
            seats |> List.filter (fun s -> s.Status = SeatStatus.Reserved)
        )

    // Thread-safe seat reservation check
    let checkSeatsAvailable seatPositions seats =
        lock bookingLock (fun () ->
            seatPositions
            |> List.forall (fun (r, c) ->
                match selectSeat r c seats with
                | Some seat -> seat.Status = SeatStatus.Available
                | None -> false)
        )

    // عرض خريطة الكراسي (اختياري)
    let displaySeatMap seats rows cols =
        printfn "\n=== Seat Map ==="
        printfn "Legend: [A] = Available, [R] = Reserved\n"
        for r in 1..rows do
            printf "Row %d: " r
            for c in 1..cols do
                match selectSeat r c seats with
                | Some seat when seat.Status = SeatStatus.Available -> printf "[A] "
                | Some seat when seat.Status = SeatStatus.Reserved -> printf "[R] "
                | _ -> printf "[?] "
            printfn ""
