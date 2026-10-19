using AutoMapper;
using Somnolencia.Application.DTOs;
using Somnolencia.Domain.Entities;

namespace Somnolencia.Application.Mappings;

public sealed class SomnolenciaMappingProfile : Profile
{
    public SomnolenciaMappingProfile()
    {
        CreateMap<EventoCierreOcular, AlertaSomnolenciaRespuestaDto>()
            .ForCtorParam("EventoId", opt => opt.MapFrom(src => src.Id))
            .ForCtorParam("ConductorId", opt => opt.MapFrom(src => src.ConductorId))
            .ForCtorParam("NombreConductor", opt => opt.MapFrom(src => src.NombreConductor))
            .ForCtorParam("NivelSeveridad", opt => opt.MapFrom(src => src.NivelSeveridad))
            .ForCtorParam("DuracionCierreMs", opt => opt.MapFrom(src => src.DuracionCierreMs))
            .ForCtorParam("ValorEar", opt => opt.MapFrom(src => src.ValorEar))
            .ForCtorParam("ValorMar", opt => opt.MapFrom(src => src.ValorMar))
            .ForCtorParam("PuntajeSomnolencia", opt => opt.MapFrom(src => src.PuntajeSomnolencia))
            .ForCtorParam("DiagnosticoGoogleAI", opt => opt.MapFrom(src => src.DiagnosticoContextualIA))
            .ForCtorParam("FechaRegistro", opt => opt.MapFrom(src => src.FechaRegistro));
    }
}