using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class UpdateProductFormatRequestValidator : AbstractValidator<UpdateProductFormatRequest>
{
    public UpdateProductFormatRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Nazwa formatu jest wymagana.")
            .MaximumLength(200)
            .WithMessage("Maksymalna długość to 200 znaków.");
    }
}
