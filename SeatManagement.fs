namespace CinemaSeatBooking

module SeatManagement =

    open CinemaSeatBooking

    // Initialize الكراسي
    let initializeSeatLayout rows cols =
        [ for r in 1..rows do
            for c in 1..cols do
                yield { Row = r; Col = c; Status = SeatStatus.Available } ]

    // اختار كرسي معين
    let selectSeat row col seats =
        seats |> List.tryFind (fun s -> s.Row = row && s.Col = col)

    // منع الحجز المكرر
    let preventDoubleBooking seat =
        seat.Status <> SeatStatus.Reserved

    // عرض الكراسي المتاحة
    let getAvailableSeats seats =
        seats |> List.filter (fun s -> s.Status = SeatStatus.Available)

    // عرض الكراسي المحجوزة
    let getReservedSeats seats =
        seats |> List.filter (fun s -> s.Status = SeatStatus.Reserved)

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
