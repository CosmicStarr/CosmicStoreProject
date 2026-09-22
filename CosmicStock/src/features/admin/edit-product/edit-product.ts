import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormArray, FormControl, Validators } from '@angular/forms';
import { AdminProductService } from '../../../core/services/admin-product';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { ICjVariant, IEditProduct } from '../../models/editProduct';

type PendingDelete = { type: 'product' } | { type: 'image'; index: number };

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
  protected readonly saveMessage = signal<string | null>(null);
  protected readonly isError = signal(false);
  protected readonly saving = signal(false);
  protected readonly pendingDelete = signal<PendingDelete | null>(null);
  protected readonly deleting = signal(false);
  protected readonly categories = signal<string[]>([]);
  protected readonly addingNewCategory = signal(false);
  protected readonly newCategoryOption = '__new__';
  protected productForm: FormGroup = new FormGroup({});

  ngOnInit(): void {
    this.isCreate.set(this.route.snapshot.data['mode'] === 'create');
    this.productId = this.route.snapshot.paramMap.get('id') || '';
    this.readNavigationNotice();

    this.productForm = new FormGroup({
      id: new FormControl(this.productId),
      cjProductId: new FormControl(''),
      nameEn: new FormControl('', Validators.required),
      descriptionEn: new FormControl(''),
      shortDescription: new FormControl(''),
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

    this.loadCategories();

    if (!this.isCreate()) {
      this.productForm.patchValue({ isFeatured: false });
      this.getProduct();
    }
  }

  get productImagesArray(): FormArray {
    return this.productForm.get('productImages') as FormArray;
  }

  onCategorySelect(value: string): void {
    if (value === this.newCategoryOption) {
      this.addingNewCategory.set(true);
      this.productForm.patchValue({ category: '' });
      return;
    }

    this.addingNewCategory.set(false);
    this.productForm.patchValue({ category: value });
  }

  onNewCategoryInput(value: string): void {
    this.productForm.patchValue({ category: value });
  }

  private loadCategories(): void {
    this.productService.getCategories().subscribe({
      next: (categories) => {
        const names = [...new Set(categories.map((name) => name.trim()).filter(Boolean))]
          .sort((left, right) => left.localeCompare(right));
        this.categories.set(names);
        this.ensureCategoryOption(this.productForm.get('category')?.value);
      },
      error: () => this.categories.set([]),
    });
  }

  private ensureCategoryOption(name: string | null | undefined): void {
    const category = name?.trim();
    if (!category || category === this.newCategoryOption) {
      this.addingNewCategory.set(false);
      return;
    }

    this.addingNewCategory.set(false);
    if (this.categories().some((item) => item.toLowerCase() === category.toLowerCase())) {
      return;
    }

    this.categories.update((items) =>
      [...items, category].sort((left, right) => left.localeCompare(right))
    );
  }

  loadFromCj(): void {
    const pid = String(this.productForm.get('cjProductId')?.value ?? '').trim();
    if (!pid || this.loadingFromCj()) return;

    this.loadingFromCj.set(true);
    this.saveMessage.set(null);
    this.adminCj.previewProduct(pid).subscribe({
      next: (preview) => {
        this.loadingFromCj.set(false);
        this.isError.set(false);
        this.cjVariants.set(preview.variants ?? []);
        this.productForm.patchValue({
          cjProductId: preview.cjProductId || pid,
          nameEn: preview.nameEn,
          sku: preview.sku,
          sellPrice: preview.sellPrice,
          bigImage: preview.bigImage ?? '',
          descriptionEn: preview.descriptionEn ?? '',
          shortDescription: preview.shortDescription?.trim() ?? '',
          category: preview.category ?? '',
        });
        this.ensureCategoryOption(preview.category);

        if (this.isCreate()) {
          this.productImagesArray.clear();
        }
        for (const variant of preview.variants ?? []) {
          this.addImage(
            variant.imageUrl ?? '',
            variant.sku,
            variant.variantName,
            undefined,
            undefined,
            variant.sku,
            variant.sellPrice,
            !this.isCreate(),
          );
        }

        const action = this.isCreate() ? 'Loaded' : 'Added';
        this.saveMessage.set(
          `${action} ${this.cjVariants().length} variants for ${preview.cjProductId || pid}. Edit properties, then save or publish to put them on the storefront.`,
        );
      },
      error: (err: { error?: { message?: string } }) => {
        this.loadingFromCj.set(false);
        this.isError.set(true);
        this.saveMessage.set(err.error?.message ?? 'Failed to load CJ product.');
      }
    });
  }

  addImage(
    imageString: string,
    skuPhoto?: string,
    typeName?: string,
    pictureId?: number,
    productTypeId?: number,
    typeSku?: string,
    typePrice?: number,
    isStorefrontDraft = false,
  ): void {
    const url = imageString?.trim();
    if (!url) return;

    const sku = (typeSku ?? skuPhoto)?.trim() || String(this.productForm.get('sku')?.value ?? '');
    const name = typeName?.trim() ?? '';
    const price = Number(typePrice) > 0 ? Number(typePrice) : 0;
    const existing = this.productImagesArray.controls.find(
      (group) => group.get('photoUrl')?.value === url
    );
    if (existing) {
      const typeGroup = existing.get('productType');
      if (name && !String(typeGroup?.get('name')?.value ?? '').trim()) {
        typeGroup?.get('name')?.setValue(name);
      }
      if (sku && !String(typeGroup?.get('sku')?.value ?? '').trim()) {
        typeGroup?.get('sku')?.setValue(sku);
      }
      if (price && !Number(typeGroup?.get('price')?.value)) {
        typeGroup?.get('price')?.setValue(price);
      }
      if (productTypeId && !Number(typeGroup?.get('id')?.value)) {
        typeGroup?.get('id')?.setValue(productTypeId);
      }
      if (productTypeId && !Number(existing.get('productTypeId')?.value)) {
        existing.get('productTypeId')?.setValue(productTypeId);
      }
      if (sku && !String(existing.get('skuPhoto')?.value ?? '').trim()) {
        existing.get('skuPhoto')?.setValue(sku);
      }
      if (pictureId && !Number(existing.get('id')?.value)) {
        existing.get('id')?.setValue(pictureId);
      }
      if (isStorefrontDraft && !existing.get('isStorefrontDraft')?.value) {
        existing.get('isStorefrontDraft')?.setValue(true);
      }
      return;
    }

    this.productImagesArray.push(new FormGroup({
      id: new FormControl(pictureId ?? 0),
      productId: new FormControl(this.productId),
      photoUrl: new FormControl(url),
      skuPhoto: new FormControl(sku),
      productTypeId: new FormControl(productTypeId ?? 0),
      isStorefrontDraft: new FormControl(isStorefrontDraft),
      productType: new FormGroup({
        id: new FormControl(productTypeId ?? 0),
        name: new FormControl(name),
        sku: new FormControl(sku),
        price: new FormControl(price),
      }),
    }));
  }

  requestDeleteImage(index: number): void {
    if (index < 0 || index >= this.productImagesArray.length) return;
    this.pendingDelete.set({ type: 'image', index });
  }

  requestDeleteProduct(): void {
    if (this.isCreate() || !this.productId) return;
    this.pendingDelete.set({ type: 'product' });
  }

  cancelDelete(): void {
    if (this.deleting()) return;
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const pending = this.pendingDelete();
    if (!pending || this.deleting()) return;

    if (pending.type === 'image') {
      this.deleteImage(pending.index);
      return;
    }

    this.deleteProduct();
  }

  galleryImageAlt(index: number): string {
    const group = this.productImagesArray.at(index);
    const type = String(group.get('productType')?.get('name')?.value ?? '').trim();
    const name = String(this.productForm.get('nameEn')?.value ?? '').trim() || 'Product';
    return type ? `${name} — ${type}` : `${name} image ${index + 1}`;
  }

  getProduct() {
    this.productService.getProductById(this.productId).subscribe({
      next: (product) => {
        this.productForm.patchValue({
          id: product.id,
          cjProductId: product.id,
          nameEn: product.nameEn,
          sku: product.sku,
          sellPrice: product.sellPrice,
          bigImage: product.bigImage ?? '',
          category: product.category ?? '',
          descriptionEn: product.descriptionEn ?? '',
          shortDescription: product.shortDescription?.trim() ?? '',
          isFeatured: product.isFeatured,
          isNewArrival: product.isNewArrival,
          isTopSelling: product.isTopSelling,
          stockQuantity: product.stockQuantity,
        });
        this.ensureCategoryOption(product.category);

        this.productImagesArray.clear();
        const typeFor = (picture?: typeof product.pictures[number]) =>
          picture?.productType
          ?? product.types?.find((item) => !!item.id && item.id === picture?.productTypeId)
          ?? product.types?.find((item) => !!item.sku && item.sku === picture?.skuPhoto);

        if (product.bigImage) {
          const hero = (product.pictures ?? []).find(
            (picture) => picture.photoUrl?.trim() === product.bigImage?.trim()
          );
          const heroType = typeFor(hero);
          this.addImage(
            product.bigImage,
            hero?.skuPhoto ?? product.sku,
            heroType?.name ?? hero?.type,
            hero?.id,
            heroType?.id ?? hero?.productTypeId,
            heroType?.sku,
            heroType?.price,
            hero?.isStorefrontDraft,
          );
        }
        for (const picture of product.pictures ?? []) {
          const type = typeFor(picture);
          this.addImage(
            picture.photoUrl,
            picture.skuPhoto,
            type?.name ?? picture.type,
            picture.id,
            type?.id ?? picture.productTypeId,
            type?.sku,
            type?.price,
            picture.isStorefrontDraft,
          );
        }
      },
      error: (err) => console.error('Failed to load product metadata', err)
    });
  }

  onSubmit() {
    if (this.productForm.invalid) return;

    this.saving.set(true);
    this.saveMessage.set(null);
    const mainImage = String(this.productForm.get('bigImage')?.value ?? '').trim();
    if (mainImage) {
      this.addImage(mainImage);
    }

    if (this.isCreate()) {
      this.productService.createProduct(this.formPayload()).subscribe({
        next: (created) => {
          this.saving.set(false);
          const name = created.nameEn?.trim() || 'Product';
          void this.router.navigate(['/admin/dashboard'], {
            state: { notice: `${name} was added to the admin catalog. Publish it when you want it on the storefront.` },
          });
        },
        error: (err: { error?: { message?: string } }) => {
          this.saving.set(false);
          this.isError.set(true);
          this.saveMessage.set(err.error?.message ?? 'Failed to add product.');
        }
      });
      return;
    }

    this.productService.updateProduct(this.productId, this.formPayload()).subscribe({
      next: () => {
        this.saving.set(false);
        this.saveMessage.set('Product saved.');
        this.isError.set(false);
      },
      error: () => {
        this.saving.set(false);
        this.saveMessage.set('Failed to save product.');
        this.isError.set(true);
      }
    });
  }

  publishToStore() {
    this.productService.publishProduct(this.productId, this.formPayload()).subscribe({
      next: () => {
        this.saveMessage.set('Product published to storefront from CJ catalog.');
        this.isError.set(false);
      },
      error: () => {
        this.saveMessage.set('Publish failed.');
        this.isError.set(true);
      }
    });
  }

  private deleteImage(index: number): void {
    const group = this.productImagesArray.at(index);
    const pictureId = Number(group.get('id')?.value ?? 0);
    const photoUrl = String(group.get('photoUrl')?.value ?? '').trim();

    if (this.isCreate() || !this.productId) {
      this.removeImageFromForm(index, photoUrl);
      this.pendingDelete.set(null);
      this.saveMessage.set('Image removed.');
      this.isError.set(false);
      return;
    }

    this.deleting.set(true);
    this.productService.deleteProductImage(this.productId, pictureId > 0 ? pictureId : undefined, photoUrl).subscribe({
      next: (product) => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        this.removeImageFromForm(index, photoUrl);
        if (product.bigImage !== undefined) {
          this.productForm.patchValue({ bigImage: product.bigImage ?? '' });
        }
        this.saveMessage.set('Image deleted.');
        this.isError.set(false);
      },
      error: (err: { error?: { message?: string } }) => {
        this.deleting.set(false);
        this.isError.set(true);
        this.saveMessage.set(err.error?.message ?? 'Could not delete that image.');
      },
    });
  }

  private deleteProduct(): void {
    this.deleting.set(true);
    this.productService.deleteProduct(this.productId).subscribe({
      next: () => {
        this.deleting.set(false);
        this.pendingDelete.set(null);
        void this.router.navigate(['/admin/dashboard'], {
          state: { notice: 'Product deleted from the storefront.' },
        });
      },
      error: (err: { error?: { message?: string } }) => {
        this.deleting.set(false);
        this.isError.set(true);
        this.saveMessage.set(err.error?.message ?? 'Could not delete this product.');
      },
    });
  }

  private removeImageFromForm(index: number, photoUrl: string): void {
    this.productImagesArray.removeAt(index);
    const currentMain = String(this.productForm.get('bigImage')?.value ?? '').trim();
    if (currentMain && currentMain === photoUrl) {
      const nextUrl = String(this.productImagesArray.at(0)?.get('photoUrl')?.value ?? '').trim();
      this.productForm.patchValue({ bigImage: nextUrl });
    }
  }

  private readNavigationNotice(): void {
    const state = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as {
      notice?: string;
      noticeError?: boolean;
    } | undefined;

    if (state?.notice) {
      this.saveMessage.set(state.notice);
      this.isError.set(!!state.noticeError);
    }
  }

  private formPayload(): IEditProduct {
    const raw = this.productForm.getRawValue() as IEditProduct;
    const cjProductId = String(raw.cjProductId ?? '').trim();
    return {
      ...raw,
      shortDescription: String(raw.shortDescription ?? '').trim() || undefined,
      cjProductId: cjProductId || undefined,
      variants: this.cjVariants(),
      productImages: (raw.productImages ?? []).map((image) => {
        const typeName = String(image.productType?.name ?? '').trim();
        const typeSku = String(image.productType?.sku ?? image.skuPhoto ?? '').trim();
        return {
          ...image,
          skuPhoto: typeSku || image.skuPhoto,
          productTypeId: image.productType?.id || image.productTypeId,
          productType: typeName || typeSku
            ? {
                id: image.productType?.id || image.productTypeId,
                name: typeName,
                sku: typeSku,
                price: Number(image.productType?.price) > 0 ? Number(image.productType?.price) : 0,
              }
            : undefined,
        };
      }),
    };
  }
}
