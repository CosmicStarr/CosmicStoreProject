import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { environment } from '../../env/environment';
import { ICategorySummary, IProductResponse } from '../../features/models/productResponse';
import { map, Observable } from 'rxjs';
import { sunParams } from '../../features/models/paramOptions';
import { PaginatedResults } from '../../features/models/pagination';

@Injectable({
  providedIn: 'root'
})
export class StoreProductsService {
  private http = inject(HttpClient);
  private apiUrl = environment.baseUrl;
  PaginatedResult?: PaginatedResults<IProductResponse[]> = new PaginatedResults<IProductResponse[]>();
  products: IProductResponse[] = [];

  getJoinedProducts(sun: sunParams) {
    let params = new HttpParams();

    if (sun.sort) params = params.set('sort', sun.sort);
    if (sun.category) params = params.set('Category', sun.category);
    if (sun.search) params = params.set('Search', sun.search);
    if (sun.minPrice !== undefined) params = params.set('MinPrice', sun.minPrice.toString());
    if (sun.maxPrice !== undefined) params = params.set('MaxPrice', sun.maxPrice.toString());
    params = params.set('pageNumber', sun.pageNumber.toString());
    params = params.set('pageSize', sun.pageSize.toString());

    return this.http.get<IProductResponse[]>(`${this.apiUrl}Products/joined-products`, { observe: 'response', params })
      .pipe(
        map((response: HttpResponse<IProductResponse[]>) => {
          const paginationHeader = response.headers.get('X-Pagination');

          if (!this.PaginatedResult) {
            this.PaginatedResult = new PaginatedResults<IProductResponse[]>();
          }

          if (paginationHeader) {
            this.PaginatedResult.Pagination = JSON.parse(paginationHeader);
          }

          this.PaginatedResult.result = response.body || [];
          return this.PaginatedResult;
        })
      );
  }

  getCategories(): Observable<ICategorySummary[]> {
    return this.http.get<ICategorySummary[]>(`${this.apiUrl}Products/categories`);
  }

  getProductById(productId: string): Observable<IProductResponse> {
    return this.http.get<IProductResponse>(`${this.apiUrl}Products/${productId}`);
  }

  getRelatedProducts(productId: string, limit = 4): Observable<IProductResponse[]> {
    return this.http.get<IProductResponse[]>(`${this.apiUrl}Products/${productId}/related`, {
      params: { limit: limit.toString() },
    });
  }

  getHighlightedProducts(highlightType: string): Observable<IProductResponse[]> {
    const params = new HttpParams().set('highlightType', highlightType);
    return this.http.get<IProductResponse[]>(`${this.apiUrl}Home/highlighted/${highlightType}`);
  }
}
