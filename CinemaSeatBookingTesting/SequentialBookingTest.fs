namespace CinemaSeatBookingTesting

module SequentialBookingTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic

    type SequentialBookingTest() =
        [<Fact>]
        member _.``Sequential bookings by different users`` () =
            let seats = initializeSeatLayout 5 5
            
            // First user books seat (1,1)
            match reserveSeats [ (1,1) ] seats with
            | Ok updatedSeats1 ->
                let bookedSeat1 = updatedSeats1 |> List.find (fun s -> s.Row = 1 && s.Col = 1)
                Assert.Equal(SeatStatus.Reserved, bookedSeat1.Status)
                
                // Second user books seat (2,2)
                match reserveSeats [ (2,2) ] updatedSeats1 with
                | Ok updatedSeats2 ->
                    let bookedSeat2 = updatedSeats2 |> List.find (fun s -> s.Row = 2 && s.Col = 2)
                    Assert.Equal(SeatStatus.Reserved, bookedSeat2.Status)
                    
                    // Verify both seats are reserved
                    let bookedSeat1Final = updatedSeats2 |> List.find (fun s -> s.Row = 1 && s.Col = 1)
                    Assert.Equal(SeatStatus.Reserved, bookedSeat1Final.Status)
                    
                    // Third user books seat (3,3)
                    match reserveSeats [ (3,3) ] updatedSeats2 with
                    | Ok updatedSeats3 ->
                        let bookedSeat3 = updatedSeats3 |> List.find (fun s -> s.Row = 3 && s.Col = 3)
                        Assert.Equal(SeatStatus.Reserved, bookedSeat3.Status)
                        
                        // Verify all three seats are reserved
                        let reservedSeats = updatedSeats3 |> List.filter (fun s -> s.Status = SeatStatus.Reserved)
                        Assert.Equal(3, reservedSeats.Length)
                    | Error _ ->
                        Assert.True(false, "Third booking should have succeeded")
                | Error _ ->
                    Assert.True(false, "Second booking should have succeeded")
            | Error _ ->
                Assert.True(false, "First booking should have succeeded")
