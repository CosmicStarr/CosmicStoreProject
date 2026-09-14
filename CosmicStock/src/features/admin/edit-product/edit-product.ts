import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormArray, FormControl, Validators } from '@angular/forms';
import { AdminProductService } from '../../../core/services/admin-product';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { ICjVariant, IEditProduct } from '../../models/editProduct';

@Component({
  selector: 'app-edit-product',
  imports: [ReactiveFormsModule],
  templateUrl: '../edit-product/edit-product.html',
  styleUrls: ['../edit-product/edit-product.scss']
})
export class EditProductComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private productService = inject(AdminProductService);
  private adminCj = inject(AdminCjService);
  productId = '';
  protected readonly isCreate = signal(false);
  protected readonly loadingFromCj = signal(false);
  protected readonly cjVariants = signal<ICjVariant[]>([]);
  protected productForm: FormGroup = new FormGroup({});
  saveMessage: string | null = null;
  isError = false;
  saving = false;

  ngOnInit(): void {
    this.isCreate.set(this.route.snapshot.data['mode'] === 'create');
    this.productId = this.route.snapshot.paramMap.get('id') || '';

    this.productForm = new FormGroup({
      id: new FormControl(this.productId),
      cjProductId: new FormControl(''),
      nameEn: new FormControl('', Validators.required),
      descriptionEn: new FormControl(''),
      sku: new FormControl('', Validators.required),
      isFeatured: new FormControl(true),
      isNewArrival: new FormControl(false),
      isTopSelling: new FormControl(false),
      sellPrice: new FormControl('', [Validators.required, Validators.min(0)]),
      stockQuantity: new FormControl(50, [Validators.required, Validators.min(0)]),
      category: new FormControl(''),
      bigImage: new FormControl(''),
      productImages: new FormArray([])
    });

    if (!this.isCreate()) {
      this.productForm.patchValue({ isFeatured: false });
      this.getProduct();
    }
  }

  get productImagesArray(): FormArray {
    return this.productForm.get('productImages') as FormArray;
  }

  loadFromCj(): void {
    const pid = String(this.productForm.get('cjProductId')?.value ?? '').trim();
    if (!pid || this.loadingFromCj()) return;

    this.loadingFromCj.set(true);
    this.saveMessage = null;
    this.adminCj.previewProduct(pid).subscribe({
      next: (preview) => {
        this.loadingFromCj.set(false);
        this.isError = false;
        this.cjVariants.set(preview.variants ?? []);
        this.productForm.patchValue({
          cjProductId: preview.cjProductId || pid,
          nameEn: preview.nameEn,
          sku: preview.sku,
          sellPrice: preview.sellPrice,
          bigImage: preview.bigImage ?? '',
          descriptionEn: preview.descriptionEn ?? '',
          category: preview.category ?? '',
        });

        this.productImagesArray.clear();
        for (const variant of preview.variants ?? []) {
          this.addImage(variant.imageUrl ?? '', variant.sku, variant.variantName);
        }

        this.saveMessage = `Loaded ${this.cjVariants().length} variants for ${preview.cjProductId || pid}.`;
      },
      error: (err: { error?: { message?: string } }) => {
        this.loadingFromCj.set(false);
        this.isError = true;
        this.saveMessage = err.error?.message ?? 'Failed to load CJ product.';
      }
    });
  }

  addImage(imageString: string, skuPhoto?: string, type?: string): void {
    const url = imageString?.trim();
    if (!url) return;

    const sku = skuPhoto?.trim() || String(this.productForm.get('sku')?.value ?? '');
    const imageType = type?.trim() ?? '';
    const existing = this.productImagesArray.controls.find(
      (group) => group.get('photoUrl')?.value === url && group.get('skuPhoto')?.value === sku
    );
    if (existing) {
      if (imageType && !String(existing.get('type')?.value ?? '').trim()) {
        existing.get('type')?.setValue(imageType);
      }
      return;
    }

    this.productImagesArray.push(new FormGroup({
      productId: new FormControl(this.productId),
      photoUrl: new FormControl(url),
      skuPhoto: new FormControl(sku),
      type: new FormControl(imageType)
    }));
  }

  galleryImageAlt(index: number): string {
    const group = this.productImagesArray.at(index);
    const type = String(group.get('type')?.value ?? '').trim();
    const name = String(this.productForm.get('nameEn')?.value ?? '').trim() || 'Product';
    return type ? `${name} — ${type}` : `${name} image ${index + 1}`;
  }

  getProduct() {
    this.productService.getProductById(this.productId).subscribe({
      next: (product) => {
        this.productForm.patchValue({
          id: product.id,
          nameEn: product.nameEn,
          sku: product.sku,
          sellPrice: product.sellPrice,
          bigImage: product.bigImage ?? '',
          category: product.category ?? '',
          descriptionEn: product.descriptionEn ?? '',
          isFeatured: product.isFeatured,
          isNewArrival: product.isNewArrival,
          isTopSelling: product.isTopSelling,
          stockQuantity: product.stockQuantity,
        });

        this.productImagesArray.clear();
        if (product.bigImage) {
          const hero = (product.pictures ?? []).find(
            (picture) => picture.photoUrl?.trim() === product.bigImage?.trim()
          );
          this.addImage(product.bigImage, hero?.skuPhoto ?? product.sku, hero?.type);
        }
        for (const picture of product.pictures ?? []) {
          this.addImage(picture.photoUrl, picture.skuPhoto, picture.type);
        }
      },
      error: (err) => console.error('Failed to load product metadata', err)
    });
  }

  onSubmit() {
    if (this.productForm.invalid) return;

    this.saving = true;
    this.saveMessage = null;
    const mainImage = String(this.productForm.get('bigImage')?.value ?? '').trim();
    if (mainImage) {
      this.addImage(mainImage);
    }

    if (this.isCreate()) {
      this.productService.createProduct(this.formPayload()).subscribe({
        next: (created) => {
          this.saving = false;
          this.isError = false;
          this.saveMessage = 'Product added to the storefront.';
          void this.router.navigate(['/admin/edit-Product', created.id]);
        },
        error: (err: { error?: { message?: string } }) => {
          this.saving = false;
          this.isError = true;
          this.saveMessage = err.error?.message ?? 'Failed to add product.';
        }
      });
      return;
    }

    this.productService.updateProduct(this.productId, this.formPayload()).subscribe({
      next: () => {
        this.saving = false;
        this.saveMessage = 'Product saved to storefront.';
        this.isError = false;
      },
      error: () => {
        this.saving = false;
        this.saveMessage = 'Failed to save product.';
        this.isError = true;
      }
    });
  }

  publishToStore() {
    this.productService.publishProduct(this.productId, this.formPayload()).subscribe({
      next: () => {
        this.saveMessage = 'Product published to storefront from CJ catalog.';
        this.isError = false;
      },
      error: () => {
        this.saveMessage = 'Publish failed.';
        this.isError = true;
      }
    });
  }

  private formPayload(): IEditProduct {
    const raw = this.productForm.getRawValue() as IEditProduct;
    const cjProductId = String(raw.cjProductId ?? '').trim();
    return {
      ...raw,
      cjProductId: cjProductId || undefined,
      variants: this.cjVariants()
    };
  }
}
