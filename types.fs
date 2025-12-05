namespace CinemaSeatBooking

open System

// حالة الكرسي
type SeatStatus = 
    | Available 
    | Reserved

// تمثيل كل كرسي
type Seat = { 
    Row: int
    Col: int
    Status: SeatStatus 
}

// التيكت
type Ticket = { 
    Id: Guid
    GridId: string  // Special ID representing the grid configuration (e.g., "5x10")
    Seats: Seat list
    CreatedAt: DateTime 
}

// حالة السينما الكاملة
type CinemaState = {
    Seats: Seat list
    Tickets: Ticket list
}

// طلب الحجز
type ReserveRequest = {
    SeatPositions: (int * int) list
}

// الرد من الـ API
type ApiResponse<'T> = {
    Success: bool
    Data: 'T option
    Error: string option
}




