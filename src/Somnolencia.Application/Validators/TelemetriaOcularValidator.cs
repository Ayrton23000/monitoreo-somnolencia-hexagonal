using System;
using FluentValidation;
using Somnolencia.Application.DTOs;

namespace Somnolencia.Application.Validators;

public sealed class TelemetriaOcularValidator : AbstractValidator<TelemetriaOcularEntradaDto>
{
    public TelemetriaOcularValidator()
    {
        RuleFor(x => x.ConductorId)
            .NotEmpty().WithMessage("El identificador del conductor es obligatorio.");

        RuleFor(x => x.DuracionCierreMs)
            .GreaterThanOrEqualTo(0).WithMessage("La duracion no puede ser negativa.")
            .LessThanOrEqualTo(60000).WithMessage("Duracion fuera de rango permitido.");

        RuleFor(x => x.FotogramaBase64)
            .NotEmpty().WithMessage("El fotograma es obligatorio.")
            .Must(EsImagenValida).WithMessage("El archivo enviado no posee una firma binaria JPEG o PNG valida.");
    }

    private static bool EsImagenValida(string base64)
    {
        try
        {
            var limpia = base64.Contains(',') ? base64.Split(',')[1] : base64;
            var bytes = Convert.FromBase64String(limpia);
            if (bytes.Length < 4) return false;

            bool esJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
            bool esPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
            return esJpeg || esPng;
        }
        catch
        {
            return false;
        }
    }
}