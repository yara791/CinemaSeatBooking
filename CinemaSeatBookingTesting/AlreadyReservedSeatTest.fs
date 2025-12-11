namespace CinemaSeatBookingTesting

module AlreadyReservedSeatTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic

    type AlreadyReservedSeatTest() =
        [<Fact>]
        member _.``Detect already reserved seat`` () =
            let seats = initializeSeatLayout 4 4
            
            // Reserve seats (1,1) and (1,2)
            match reserveSeats [ (1,1); (1,2) ] seats with
            | Ok updatedSeats ->
                // Verify seats are reserved
                let reserved1 = updatedSeats |> List.find (fun s -> s.Row = 1 && s.Col = 1)
                let reserved2 = updatedSeats |> List.find (fun s -> s.Row = 1 && s.Col = 2)
                Assert.Equal(SeatStatus.Reserved, reserved1.Status)
                Assert.Equal(SeatStatus.Reserved, reserved2.Status)
                
                // Try to book seat (1,1) which is already reserved
                match reserveSeats [ (1,1) ] updatedSeats with
                | Ok _ ->
                    Assert.True(false, "Booking already reserved seat should have failed")
                | Error unavailableSeats ->
                    // Should contain the already reserved seat (1,1)
                    Assert.Contains((1, 1), unavailableSeats)
                    
                // Try to book multiple seats including an already reserved one
                match reserveSeats [ (2,1); (1,2); (2,2) ] updatedSeats with
                | Ok _ ->
                    Assert.True(false, "Booking should have failed due to already reserved seat (1,2)")
                | Error unavailableSeats ->
                    // Should contain the already reserved seat (1,2)
                    Assert.Contains((1, 2), unavailableSeats)
            | Error _ ->
                Assert.True(false, "Initial booking should have succeeded")
