using Microsoft.AspNetCore.Components;
using MudBlazor;
using KanbanLite.Contracts;
using KanbanLite.Application.Services;
using KanbanLite.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace KanbanLite.Web.Pages.Manager;

public partial class CreateOrder
{
    [Inject] private ISessionService SessionService { get; set; } = null!;
    
    private CreateOrderFormModel formModel = new();
    private DateTime? selectedDueDate = DateTime.Now.AddDays(7);
    private DateTime minDueDate = DateTime.Now.AddDays(7);
    private List<ProductFormatLookupDto> productFormats = new();
    private bool isSubmitting = false;
    private string validationMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (!SessionService.IsAuthenticated)
        {
            Navigation.NavigateTo("/login", replace: true);
            return;
        }
        
        if (!SessionService.IsInRole("Manager"))
        {
            Navigation.NavigateTo("/kanban", replace: true);
            return;
        }
        
        try
        {
            var result = await ProductFormatService.GetActiveLookupAsync();
            if (result.IsSuccess)
            {
                productFormats = result.Value.ToList();
            }
            else
            {
                Snackbar.Add("Błąd podczas ładowania formatów produktów", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnValidSubmit()
    {
        if (isSubmitting) return;

        try
        {
            isSubmitting = true;
            validationMessage = string.Empty;

            // Walidacja po stronie klienta
            if (!ValidateRequest())
            {
                return;
            }

            // Konwersja DateTime na DateOnly i CreateOrderRequest
            var createRequest = new KanbanLite.Contracts.CreateOrderRequest(
                formModel.OrderNumber,
                formModel.Quantity,
                formModel.ProductFormatId,
                DateOnly.FromDateTime(selectedDueDate!.Value)
            );

            var result = await OrderService.CreateAsync(createRequest);

            if (result.IsSuccess)
            {
                Snackbar.Add("Zlecenie zostało pomyślnie utworzone!", Severity.Success);
                Navigation.NavigateTo("/kanban");
            }
            else
            {
                validationMessage = result.Error.Message;
            }
        }
        catch (Exception ex)
        {
            validationMessage = $"Wystąpił nieoczekiwany błąd: {ex.Message}";
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private bool ValidateRequest()
    {
        // Walidacja numeru zlecenia
        if (string.IsNullOrWhiteSpace(formModel.OrderNumber))
        {
            validationMessage = "Numer zlecenia jest wymagany.";
            return false;
        }

        if (formModel.OrderNumber.Length > 100)
        {
            validationMessage = "Numer zlecenia nie może być dłuższy niż 100 znaków.";
            return false;
        }

        // Walidacja ilości
        if (formModel.Quantity < 1 || formModel.Quantity > 100000)
        {
            validationMessage = "Ilość musi być z zakresu 1-100,000 sztuk.";
            return false;
        }

        // Walidacja formatu produktu
        if (formModel.ProductFormatId <= 0)
        {
            validationMessage = "Proszę wybrać format produktu.";
            return false;
        }

        // Walidacja terminu realizacji
        if (!selectedDueDate.HasValue)
        {
            validationMessage = "Termin realizacji jest wymagany.";
            return false;
        }

        var dueDate = DateOnly.FromDateTime(selectedDueDate.Value);
        var minDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7));
        
        if (dueDate < minDate)
        {
            validationMessage = "Termin realizacji musi być co najmniej 7 dni od dzisiaj.";
            return false;
        }

        return true;
    }
}

// Model walidacji dla formularza  
public class CreateOrderFormModel
{
    [Required(ErrorMessage = "Numer zlecenia jest wymagany")]
    [StringLength(100, ErrorMessage = "Numer zlecenia nie może być dłuższy niż 100 znaków")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ilość jest wymagana")]
    [Range(1, 100000, ErrorMessage = "Ilość musi być z zakresu 1-100,000")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Format produktu jest wymagany")]
    [Range(1, long.MaxValue, ErrorMessage = "Proszę wybrać format produktu")]
    public long ProductFormatId { get; set; }
}