using AutoMapper;
using Models;
using Models.DTOs;

namespace CosmicStoreAPI.Auto;


public class AutoMapperProfiles : Profile
{
    public AutoMapperProfiles()
    {
        CreateMap<Products,ProductDTO>().ForPath(x=>x.Category,o=>o.MapFrom(x=>x.Category.Name))
                                        .ForPath(x=>x.Brand,o=>o.MapFrom(x=>x.Brand.Name));
    }
}