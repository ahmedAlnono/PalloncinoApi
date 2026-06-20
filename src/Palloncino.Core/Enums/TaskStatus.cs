namespace Palloncino.Models.Enums;

public enum TaskStatus
{
    Pending = 1,       // Not started
    InProgress = 2,    // Working on it
    Completed = 3,     // Successfully completed
    Skipped = 4,       // Skipped by authorized user
    Overdue = 5,       // Past due date not completed
    Blocked = 6        // Waiting for another task
}