namespace CinemaSeatBookingTesting

module DoubleBookingTest =
    open Xunit
    open CinemaSeatBooking
    open CinemaSeatBooking.SeatManagement 
    open CinemaSeatBooking.BookingLogic
    type DoubleBookingTest() =
      [<Fact>]
         member _.``Booking the same seat twice should fail`` () =
              let seats = initializeSeatLayout 4 4
              let firstAttempt = reserveSeats [ (1,1) ] seats
              match firstAttempt with
              | Ok updatedSeats ->
                  let secondAttempt = reserveSeats [ (1,1) ] updatedSeats
                  match secondAttempt with
                  | Error msg ->
                    Assert.True(true, $"Got expected error: {msg}")
                  | Ok _ ->
                    Assert.True(false, "Second booking of the same seat should not succeed")
              | Error _ ->
                Assert.True(false, "First booking should have succeeded")
