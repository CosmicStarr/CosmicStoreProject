export interface IPagination {
    CurrentPage?:number
    ItemsPerPage?:number
    TotalItems?:number
    TotalPages?:number
 }

 export class PaginatedResults<T>{
     result?: T;
     Pagination?:IPagination;
 }

 export class Pagination implements IPagination{
     CurrentPage?:number
     ItemsPerPage?:number
     TotalItems?:number
     TotalPages?:number
 }