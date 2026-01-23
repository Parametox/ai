using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .WithMessage("Numer zlecenia jest wymagany.")
            .MaximumLength(100)
            .WithMessage("Maksymalna długość to 100 znaków.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 100000)
            .WithMessage("Ilość musi być w zakresie 1..100000.");

        RuleFor(x => x.DueDate)
            .Must(BeAtLeastSevenDaysFromNow)
            .WithMessage("Minimalna data realizacji to dziś + 7 dni.");

        RuleFor(x => x.ProductFormatId)
            .GreaterThan(0)
            .WithMessage("Format produktu jest wymagany.");
    }

    private static bool BeAtLeastSevenDaysFromNow(DateOnly dueDate)
    {
        var minDue = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7));
        return dueDate >= minDue;
    }
}
