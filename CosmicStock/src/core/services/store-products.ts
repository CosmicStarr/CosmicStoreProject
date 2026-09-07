import { inject, Injectable, Service } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { environment } from '../../env/environment';
import { IProductResponse } from '../../features/models/productResponse';
import { map, Observable } from 'rxjs';
import { sunParams } from '../../features/models/paramOptions';
import { PaginatedResults } from '../../features/models/pagination';


@Injectable({
  providedIn: 'root' // Available globally across all standalone components
})
export class StoreProductsService {
 private http = inject(HttpClient);
 private apiUrl = environment.baseUrl;
 PaginatedResult?:PaginatedResults<IProductResponse[]> = new PaginatedResults<IProductResponse[]>()
 products:IProductResponse[]=[];
 params = new HttpParams();
 

getJoinedProducts(_sunParams: sunParams) {
  let params = new HttpParams(); // Added missing parentheses
  if (_sunParams?.sort) {
    params = params.append('sort', _sunParams.sort);
  }
  if (_sunParams?.category) {
    params = params.append('Category', _sunParams.category);
  }
  if (_sunParams?.search) {
    params = params.append('Search', _sunParams.search);
  }
  if (_sunParams?.pageNumber !== undefined && _sunParams?.pageSize !== undefined) {
    params = params.append('pageNumber', _sunParams.pageNumber.toString());
    params = params.append('pageSize', _sunParams.pageSize.toString());
  }
  // RETURN the observable here, pipe the response to extract headers
  return this.http.get<IProductResponse[]>(`${this.apiUrl}Products/joined-products`, { observe: 'response', params: params })
    .pipe(
      map((response: HttpResponse<IProductResponse[]>) => {
        // Extract the pagination header
        const paginationHeader = response.headers.get('X-Pagination');

        if(!this.PaginatedResult){
          this.PaginatedResult = new PaginatedResults<IProductResponse[]>();
        }
        
        if (paginationHeader) {
          this.PaginatedResult.Pagination = JSON.parse(paginationHeader);
        }
        // Return a combined object that your component expects
        this.PaginatedResult.result = response.body ||[]
        return this.PaginatedResult;
      })
    );
}

getProductById(productId: string): Observable<IProductResponse> {
    return this.http.get<IProductResponse>(`${this.apiUrl}Products/${productId}`);
  }

getHighlightedProducts(highlightType: string): Observable<IProductResponse[]> {
    // This creates a safe query string: ?highlightType=NewArrival
    let params = new HttpParams().set('highlightType', highlightType);
    // No pipe or map needed if the API response matches the interface exactly
    return this.http.get<IProductResponse[]>(`${this.apiUrl}Home/highlighted/${highlightType}`);
  }
}
