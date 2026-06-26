// ambient module declaration
import { PermissionBase } from "../models/Permission";

export {};

declare module "vue-router" {
  interface RouteMeta {
    requireLogin?: boolean;

    /**
     * When using `requiredPermissions` as a flat array of strings, the user's permissions
     * must include **ALL** the required permission strings to pass the validation.
     *
     * @example
     * ```ts
     * // meta
     * requiredPermissions: ["Purchase.View", "RawMaterialPurchase.View"]
     *
     * // user permissions
     * const userA = ["Purchase.View", "RawMaterialPurchase.View", "Others.Edit"]
     * const userB = ["Purchase.View", "Others.Edit"]
     *
     * userA // passes
     * userB // fails (missing "RawMaterialPurchase.View")
     * ```
     *
     * When using `requiredPermissions` as `Array<PermissionBase[]>`,
     * the permission subarrays are treated as "sets" of permissions,
     * and matched using "OR" strategy.
     *
     * Users only need to fulfill one of the sets to pass validation.
     *
     * @example
     * ```ts
     * // meta
     *  requiredPermissions: [
     *    // set A
     *    ["Purchase.View", "RawMaterialPurchase.View"],
     *    // set B
     *    ["Organization.View"],
     * ]
     *
     * // user permissions
     * const userA = ["Purchase.View", "RawMaterialPurchase.View"]
     * const userB = ["Purchase.View"]
     * const userC = ["Organization.View"]
     *
     * userA // passes set A
     * userB // fails (did not pass either set)
     * userC // passes set B
     * ```
     */
    requiredPermissions?: PermissionBase[] | Array<PermissionBase[]>;

    /**
     * String used to highlight the correct sub item in side panel.
     * This should match the "name" in child menu.
     */
    menuName?: string;
  }
}
