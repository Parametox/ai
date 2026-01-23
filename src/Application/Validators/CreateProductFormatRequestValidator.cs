using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class CreateProductFormatRequestValidator : AbstractValidator<CreateProductFormatRequest>
{
    public CreateProductFormatRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Nazwa formatu jest wymagana.")
            .MaximumLength(200)
            .WithMessage("Maksymalna długość to 200 znaków.");
    }
}
