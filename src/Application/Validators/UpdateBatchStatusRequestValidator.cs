using DataAccess.Enums;
using FluentValidation;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Validators;

public class UpdateBatchStatusRequestValidator : AbstractValidator<UpdateBatchStatusRequest>
{
    public UpdateBatchStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .WithMessage("Nieprawidłowy status batcha.");
    }
}
