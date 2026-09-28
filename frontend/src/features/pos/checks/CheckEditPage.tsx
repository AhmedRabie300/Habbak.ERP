import { useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Button } from '../../../ui-kit/Button';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { printElement } from '../../../lib/export';
import { useButtonChecker } from '../../auth/access';
import { useItemsList } from '../../inventory/items/api';
import { usePOSCategoriesList } from '../../inventory/posCategories/api';
import { useCustomersList } from '../../sales/customers/api';
import { useRedeemQRTicket } from '../qrTickets/api';
import { useCreateDrawerMovement } from '../drawerMovements/api';
import type { DrawerMovementType } from '../drawerMovements/types';
import {
  useAddCheckLine, useApplyManualDiscount, useCancelCheck, useChangeCheckOrderType, useCheck,
  useFireCheckLinesToKitchen, useHoldCheck, useRemoveCheckLine, useRemoveManualDiscount, useResumeCheck,
  useSetCheckCustomer, useSetLoyaltyRedemption, useUpdateCheckLine
} from './api';
import type { CheckLine, CheckOrderType, ManualDiscountType } from './types';

const ORDER_TYPES: CheckOrderType[] = ['DineIn', 'Takeaway', 'Delivery'];
const ALL_CATEGORY = -1;

function miniActionButtonStyle() {
  return {
    flex: 1, fontWeight: 700, fontSize: 11.5, padding: '9px 6px', borderRadius: 7,
    background: '#2C2019', border: '1px solid #40312A', color: '#fff', cursor: 'pointer'
  } as const;
}

/** /pos/checks/:id — الشاشة الرئيسية للبيع (screen #4)، مصمَّمة على غرار "شاشة البيع" في
 * Coffee_ERP_Full_System_Mockup.html: عمودين — تابات أنواع الطلب/التصنيفات + شبكة منتجات على
 * اليمين، وسلة داكنة (cart-panel) بعناصر Qty-stepper وزر دفع كبير على اليسار. شريحة الشيك/البنود
 * فقط في هذه المرحلة — الدفع الفعلي بيحصل في شاشة منفصلة (/pos/checks/:id/payment).</summary> */
export function CheckEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const checkId = Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const buttonAllowed = useButtonChecker();
  const can = (buttonCode: string) => buttonAllowed('POS_TABLE_BOARD', buttonCode);

  const { data: check, isLoading } = useCheck(checkId);
  const { data: items } = useItemsList();
  const { data: categories } = usePOSCategoriesList();
  const { data: customers } = useCustomersList();

  const holdMutation = useHoldCheck(checkId);
  const resumeMutation = useResumeCheck(checkId);
  const cancelMutation = useCancelCheck(checkId);
  const changeOrderTypeMutation = useChangeCheckOrderType(checkId);
  const addLineMutation = useAddCheckLine(checkId);
  const updateLineMutation = useUpdateCheckLine(checkId);
  const removeLineMutation = useRemoveCheckLine(checkId);
  const fireMutation = useFireCheckLinesToKitchen(checkId);
  const redeemQRTicketMutation = useRedeemQRTicket();
  const applyDiscountMutation = useApplyManualDiscount(checkId);
  const removeDiscountMutation = useRemoveManualDiscount(checkId);
  const setCustomerMutation = useSetCheckCustomer(checkId);
  const setLoyaltyRedemptionMutation = useSetLoyaltyRedemption(checkId);
  const createDrawerMovementMutation = useCreateDrawerMovement();

  const cartPanelRef = useRef<HTMLDivElement>(null);
  const [activeCategory, setActiveCategory] = useState<number>(ALL_CATEGORY);
  const [voidTarget, setVoidTarget] = useState<{ lineId: number; itemNameAr: string } | null>(null);
  const [voidReason, setVoidReason] = useState('');
  const [showScanModal, setShowScanModal] = useState(false);
  const [scanKey, setScanKey] = useState('');
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const [discountType, setDiscountType] = useState<ManualDiscountType>('Percentage');
  const [discountValue, setDiscountValue] = useState('');
  const [discountReason, setDiscountReason] = useState('');
  const [redeemPointsInput, setRedeemPointsInput] = useState('');
  const [showDrawerModal, setShowDrawerModal] = useState<DrawerMovementType | null>(null);
  const [drawerAmount, setDrawerAmount] = useState('');
  const [drawerReason, setDrawerReason] = useState('');

  const runAction = async (action: () => Promise<unknown>, successMessage?: string) => {
    try {
      await action();
      if (successMessage) showToast(successMessage, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const visibleItems = (items ?? []).filter((i) => i.isActive && i.isSellable && (activeCategory === ALL_CATEGORY || i.posCategoryId === activeCategory));
  const customerOptions = (customers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr} (${c.loyaltyPointsBalance} ${t('checkEdit.loyaltyPointsShort')})` }));

  const handleAddProduct = async (itemId: number) => {
    if (!check) return;
    const existingLine = check.lines.find((l) => l.itemId === itemId && !l.sentToKitchenAt);
    if (existingLine) {
      await runAction(() => updateLineMutation.mutateAsync({
        lineId: existingLine.id, quantity: existingLine.quantity + 1, unitPrice: existingLine.unitPrice,
        discountAmount: existingLine.discountAmount, isPriceManuallyOverridden: existingLine.isPriceManuallyOverridden
      }));
    } else {
      await runAction(() => addLineMutation.mutateAsync({ itemId, quantity: 1, discountAmount: 0 }));
    }
  };

  const handleStepQuantity = async (line: CheckLine, delta: number) => {
    const newQuantity = line.quantity + delta;
    if (newQuantity <= 0) {
      await runAction(() => removeLineMutation.mutateAsync({ lineId: line.id }));
      return;
    }
    await runAction(() => updateLineMutation.mutateAsync({
      lineId: line.id, quantity: newQuantity, unitPrice: line.unitPrice,
      discountAmount: line.discountAmount, isPriceManuallyOverridden: line.isPriceManuallyOverridden
    }));
  };

  const handleEditPrice = async (line: CheckLine, value: number) => {
    if (value < 0 || value === line.unitPrice) return;
    await runAction(() => updateLineMutation.mutateAsync({
      lineId: line.id, quantity: line.quantity, unitPrice: value, discountAmount: line.discountAmount, isPriceManuallyOverridden: true
    }));
  };

  const handleRemoveLine = async (line: CheckLine) => {
    if (line.sentToKitchenAt) {
      setVoidTarget({ lineId: line.id, itemNameAr: line.itemNameAr });
      setVoidReason('');
      return;
    }
    await runAction(() => removeLineMutation.mutateAsync({ lineId: line.id }));
  };

  const confirmVoid = async () => {
    if (!voidTarget || !voidReason.trim()) return;
    await runAction(() => removeLineMutation.mutateAsync({ lineId: voidTarget.lineId, voidReason: voidReason.trim() }));
    setVoidTarget(null);
  };

  const confirmScan = async () => {
    if (!scanKey.trim()) return;
    await runAction(() => redeemQRTicketMutation.mutateAsync({ checkId, idempotencyKey: scanKey.trim() }), t('checkEdit.scanQrSuccess'));
    setShowScanModal(false);
    setScanKey('');
  };

  const confirmApplyDiscount = async () => {
    const value = Number(discountValue);
    if (!value || value <= 0 || !discountReason.trim()) return;
    await runAction(
      () => applyDiscountMutation.mutateAsync({ type: discountType, value, reason: discountReason.trim() }),
      t('checkEdit.discountAppliedSuccess')
    );
    setShowDiscountModal(false);
    setDiscountValue('');
    setDiscountReason('');
  };

  const handleRemoveDiscount = () => runAction(() => removeDiscountMutation.mutateAsync(), t('checkEdit.discountRemovedSuccess'));

  const handleSelectCustomer = (customerId: number) =>
    runAction(() => setCustomerMutation.mutateAsync(customerId), t('checkEdit.customerSetSuccess'));

  const handleClearCustomer = () => runAction(() => setCustomerMutation.mutateAsync(null), t('checkEdit.customerClearedSuccess'));

  const handleApplyRedemption = async () => {
    const points = Number(redeemPointsInput);
    if (!points || points <= 0) return;
    await runAction(() => setLoyaltyRedemptionMutation.mutateAsync(points), t('checkEdit.redeemAppliedSuccess'));
    setRedeemPointsInput('');
  };

  const handleClearRedemption = () => runAction(() => setLoyaltyRedemptionMutation.mutateAsync(0), t('checkEdit.redeemClearedSuccess'));

  const handleReprint = () => {
    if (!cartPanelRef.current || !check) return;
    printElement(cartPanelRef.current, `${t('checkEdit.cartTitle')} — ${check.checkCode}`);
  };

  const openDrawerModal = (type: DrawerMovementType) => {
    setShowDrawerModal(type);
    setDrawerAmount('');
    setDrawerReason('');
  };

  const confirmDrawerMovement = async () => {
    if (!showDrawerModal || !check) return;
    const amount = Number(drawerAmount);
    if (!amount || amount <= 0) return;
    await runAction(
      () => createDrawerMovementMutation.mutateAsync({
        shiftId: check.shiftId, movementType: showDrawerModal, amount, reason: drawerReason.trim() || undefined
      }),
      t('drawerMovements.createSuccess')
    );
    setShowDrawerModal(null);
    setDrawerAmount('');
    setDrawerReason('');
  };

  if (isLoading || !check) {
    return <div>{t('common.loading')}</div>;
  }

  const isActive = check.status === 'Open' || check.status === 'Held';
  const hasUnsentLines = check.lines.some((l) => !l.sentToKitchenAt);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
        <h2 style={{ margin: 0 }}>
          {check.checkCode}{check.tableCode ? ` — ${t('checkEdit.table')} ${check.tableCode}` : ''}
        </h2>
        <StatusBadge status={check.status} />
      </div>

      <ActionBar
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/pos/table-board') },
          ...(isActive ? [{ key: 'scanQr', label: t('checkEdit.scanQr'), onClick: () => setShowScanModal(true) }] : []),
          ...(isActive ? [{ key: 'manualDiscount', buttonCode: 'ApplyManualDiscount', buttonScreen: 'POS_TABLE_BOARD', label: t('checkEdit.manualDiscount'), onClick: () => setShowDiscountModal(true) }] : []),
          ...(isActive && hasUnsentLines ? [{ key: 'fire', buttonCode: 'FireToKitchen', buttonScreen: 'POS_TABLE_BOARD', label: t('checkEdit.fireToKitchen'), onClick: () => runAction(() => fireMutation.mutateAsync(), t('checkEdit.fireSuccess')) }] : []),
          ...(check.status === 'Open' ? [{ key: 'hold', buttonCode: 'Hold', buttonScreen: 'POS_TABLE_BOARD', label: t('checkEdit.hold'), onClick: () => runAction(() => holdMutation.mutateAsync(), t('checkEdit.holdSuccess')) }] : []),
          ...(check.status === 'Held' ? [{ key: 'resume', buttonCode: 'Hold', buttonScreen: 'POS_TABLE_BOARD', label: t('checkEdit.resume'), onClick: () => runAction(() => resumeMutation.mutateAsync(), t('checkEdit.resumeSuccess')) }] : [])
        ]}
        destructive={isActive ? [{ key: 'cancel', buttonCode: 'Cancel', buttonScreen: 'POS_TABLE_BOARD', label: t('checkEdit.cancelCheck'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('checkEdit.cancelSuccess')), confirmMessage: t('checkEdit.cancelConfirm') }] : []}
      />

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16, minWidth: 0, alignItems: 'start' }}>
        {/* pos-main */}
        <div style={{ flex: '2 1 380px', background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', padding: '18px 20px', minWidth: 0 }}>
          <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
            {ORDER_TYPES.map((ot) => (
              <button
                key={ot}
                disabled={!isActive}
                onClick={() => runAction(() => changeOrderTypeMutation.mutateAsync(ot))}
                style={{
                  flex: 1, fontWeight: 800, fontSize: 13, padding: '10px 8px', borderRadius: 8,
                  border: `1px solid ${check.orderType === ot ? 'var(--color-navy-900)' : 'var(--color-border)'}`,
                  background: check.orderType === ot ? 'var(--color-navy-900)' : 'var(--color-surface-2)',
                  color: check.orderType === ot ? '#fff' : 'var(--color-text-muted)',
                  cursor: isActive ? 'pointer' : 'not-allowed'
                }}
              >
                {t(`checkEdit.orderType${ot}`)}
              </button>
            ))}
          </div>

          <div style={{ display: 'flex', gap: 6, overflowX: 'auto', paddingBottom: 12, marginBottom: 14, borderBottom: '1px solid var(--color-border)' }}>
            <button
              onClick={() => setActiveCategory(ALL_CATEGORY)}
              style={{
                fontWeight: 700, fontSize: 13, padding: '8px 16px', borderRadius: 'var(--radius-pill)', whiteSpace: 'nowrap',
                border: `1px solid ${activeCategory === ALL_CATEGORY ? 'var(--color-gold-500)' : 'var(--color-border)'}`,
                background: activeCategory === ALL_CATEGORY ? 'var(--color-gold-500)' : '#fff',
                color: activeCategory === ALL_CATEGORY ? '#fff' : 'var(--color-text-muted)', cursor: 'pointer'
              }}
            >
              {t('checkEdit.allCategories')}
            </button>
            {(categories ?? []).map((cat) => (
              <button
                key={cat.id}
                onClick={() => setActiveCategory(cat.id)}
                style={{
                  fontWeight: 700, fontSize: 13, padding: '8px 16px', borderRadius: 'var(--radius-pill)', whiteSpace: 'nowrap',
                  border: `1px solid ${activeCategory === cat.id ? 'var(--color-gold-500)' : 'var(--color-border)'}`,
                  background: activeCategory === cat.id ? 'var(--color-gold-500)' : '#fff',
                  color: activeCategory === cat.id ? '#fff' : 'var(--color-text-muted)', cursor: 'pointer'
                }}
              >
                {cat.nameAr}
              </button>
            ))}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: 12 }}>
            {visibleItems.map((item) => (
              <div
                key={item.id}
                onClick={() => isActive && handleAddProduct(item.id)}
                style={{
                  textAlign: 'start', border: '1px solid var(--color-border)', borderRadius: 10, padding: 14, background: '#fff',
                  cursor: isActive ? 'pointer' : 'not-allowed', display: 'flex', flexDirection: 'column', gap: 14, minHeight: 90,
                  opacity: isActive ? 1 : 0.6
                }}
              >
                <div>
                  <div style={{ fontWeight: 800, fontSize: 14 }}>{item.nameAr}</div>
                  <div style={{ fontSize: 11, color: 'var(--color-text-muted)', marginTop: 2 }}>{item.code}</div>
                </div>
                <div style={{ fontWeight: 800, fontSize: 14, color: 'var(--color-gold-600)' }}>
                  {item.defaultPrice != null ? `${item.defaultPrice.toFixed(0)} ${t('common.currency', 'ج.م')}` : t('checkEdit.priceFromList')}
                </div>
              </div>
            ))}
            {visibleItems.length === 0 && (
              <div style={{ color: 'var(--color-text-muted)', fontSize: 13, padding: 20 }}>{t('checkEdit.noProducts')}</div>
            )}
          </div>
        </div>

        {/* cart-panel */}
        <aside ref={cartPanelRef} style={{ flex: '1 1 280px', background: 'var(--color-navy-900)', color: '#fff', borderRadius: 'var(--radius-lg)', display: 'flex', flexDirection: 'column', padding: '18px 16px', minWidth: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <h3 style={{ margin: 0, fontSize: 15 }}>{t('checkEdit.cartTitle')}</h3>
            <span style={{ fontSize: 11.5, color: '#93A6B8' }}>#{check.checkCode}</span>
          </div>

          <div style={{ marginBottom: 12 }}>
            {check.customerId ? (
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#2C2019', borderRadius: 7, padding: '8px 10px', fontSize: 12 }}>
                <span>👤 {check.customerNameAr} — {(check.customerLoyaltyPointsBalance ?? 0).toFixed(0)} {t('checkEdit.loyaltyPointsShort')}</span>
                {isActive && (
                  <button onClick={handleClearCustomer} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: '#A5937E', fontSize: 12, cursor: 'pointer' }}>✕</button>
                )}
              </div>
            ) : (
              isActive && (
                <SearchableSelect
                  style={{ width: '100%' }}
                  value=""
                  onChange={(v) => v !== '' && handleSelectCustomer(Number(v))}
                  options={customerOptions}
                  placeholder={t('checkEdit.customerSearchPlaceholder')}
                />
              )
            )}
            {isActive && check.customerId && (check.customerLoyaltyPointsBalance ?? 0) > 0 && !check.loyaltyPointsToRedeem && (
              <div style={{ display: 'flex', gap: 6, marginTop: 6 }}>
                <Input
                  type="number" min={0} max={check.customerLoyaltyPointsBalance}
                  value={redeemPointsInput}
                  onChange={(e) => setRedeemPointsInput(e.target.value)}
                  placeholder={t('checkEdit.redeemPointsPlaceholder')}
                  style={{ flex: 1, background: '#2C2019', color: '#fff', border: '1px solid #40312A', fontSize: 12 }}
                />
                <Button variant="ghost" onClick={handleApplyRedemption} disabled={!can('RedeemLoyalty')}>{t('checkEdit.redeemApply')}</Button>
              </div>
            )}
          </div>

          <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 12, minHeight: 160, maxHeight: 360 }}>
            {check.lines.length === 0 && (
              <div style={{ color: '#7C90A3', fontSize: 12.5, textAlign: 'center', padding: '24px 0' }}>{t('checkEdit.cartEmpty')}</div>
            )}
            {check.lines.map((line) => (
              <div key={line.id} style={{ background: 'var(--color-navy-700)', borderRadius: 8, padding: '10px 12px', display: 'flex', flexDirection: 'column', gap: 8, fontSize: 13 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                  <div>
                    <div style={{ fontWeight: 700 }}>
                      {line.itemNameAr}
                      {line.isPriceManuallyOverridden && <span style={{ fontSize: 9, marginInlineStart: 6, color: 'var(--color-gold-500)' }}>{t('checkEdit.manualPrice')}</span>}
                    </div>
                    <div style={{ color: '#92A5B7', fontSize: 11 }}>{line.sentToKitchenAt ? t('checkEdit.sent') : t('checkEdit.notSentYet')}</div>
                  </div>
                  {isActive && (
                    <button onClick={() => handleRemoveLine(line)} disabled={!!line.sentToKitchenAt && !can('VoidSentLine')} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: '#A5937E', fontSize: 13, cursor: 'pointer' }}>✕</button>
                  )}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: '#2C2019', borderRadius: 7, padding: '3px 6px' }}>
                    <button disabled={!isActive || !!line.sentToKitchenAt} onClick={() => handleStepQuantity(line, -1)} style={{ background: 'transparent', border: 'none', color: '#fff', fontSize: 14, fontWeight: 800, cursor: 'pointer', width: 20 }}>−</button>
                    <span style={{ fontSize: 12.5, fontWeight: 800, minWidth: 16, textAlign: 'center' }}>{line.quantity}</span>
                    <button disabled={!isActive || !!line.sentToKitchenAt} onClick={() => handleStepQuantity(line, 1)} style={{ background: 'transparent', border: 'none', color: '#fff', fontSize: 14, fontWeight: 800, cursor: 'pointer', width: 20 }}>+</button>
                  </div>
                  <Input
                    type="number" step="0.01" disabled={!isActive || !!line.sentToKitchenAt || !can('EditPrice')}
                    defaultValue={line.unitPrice}
                    onBlur={(e) => handleEditPrice(line, Number(e.target.value))}
                    style={{ width: 70, background: '#2C2019', color: '#fff', border: '1px solid #40312A', fontSize: 12 }}
                  />
                  <span style={{ color: 'var(--color-gold-500)', fontWeight: 800, whiteSpace: 'nowrap' }}>{line.lineTotal.toFixed(2)}</span>
                </div>
              </div>
            ))}
          </div>

          <div style={{ borderTop: '1px solid #2C3F58', paddingTop: 12, fontSize: 13.5 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: '#B7C6D4' }}>
              <span>{t('checkEdit.subtotal')}</span><span>{check.subtotal.toFixed(2)}</span>
            </div>
            {check.discountTotal > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: 'var(--color-gold-500)' }}>
                <span>{t('checkEdit.discountTotal')}</span><span>−{check.discountTotal.toFixed(2)}</span>
              </div>
            )}
            {check.manualDiscountAmount > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: 'var(--color-gold-500)' }}>
                <span>
                  {t('checkEdit.manualDiscount')}
                  {isActive && (
                    <button onClick={handleRemoveDiscount} disabled={!can('ApplyManualDiscount')} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: '#A5937E', fontSize: 12, cursor: 'pointer', marginInlineStart: 6 }}>✕</button>
                  )}
                </span>
                <span>−{check.manualDiscountAmount.toFixed(2)}</span>
              </div>
            )}
            {check.loyaltyDiscountAmount > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', color: 'var(--color-gold-500)' }}>
                <span>
                  {t('checkEdit.loyaltyPointsRedeemed', { points: check.loyaltyPointsToRedeem })}
                  {isActive && (
                    <button onClick={handleClearRedemption} disabled={!can('RedeemLoyalty')} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: '#A5937E', fontSize: 12, cursor: 'pointer', marginInlineStart: 6 }}>✕</button>
                  )}
                </span>
                <span>−{check.loyaltyDiscountAmount.toFixed(2)}</span>
              </div>
            )}
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#fff', fontWeight: 800, fontSize: 17, borderTop: '1px dashed #2C3F58', marginTop: 6, paddingTop: 10 }}>
              <span>{t('checkEdit.total')}</span><span>{check.total.toFixed(2)}</span>
            </div>
          </div>

          {check.lines.length > 0 && (
            <div data-no-print="true" style={{ display: 'flex', gap: 8, marginTop: 12 }}>
              {can('Reprint') && <button onClick={handleReprint} style={miniActionButtonStyle()}>{t('checkEdit.reprint')}</button>}
              <button onClick={() => openDrawerModal('Drop')} style={miniActionButtonStyle()}>{t('drawerMovements.typeDrop')}</button>
              <button onClick={() => openDrawerModal('Pickup')} style={miniActionButtonStyle()}>{t('drawerMovements.typePickup')}</button>
            </div>
          )}

          {isActive && check.lines.length > 0 && (
            <button
              onClick={() => navigate(`/pos/checks/${check.id}/payment`)}
              style={{
                marginTop: 14, fontWeight: 800, fontSize: 14.5, padding: 13, borderRadius: 8,
                background: 'var(--color-gold-500)', border: '1px solid var(--color-gold-500)', color: '#fff', cursor: 'pointer'
              }}
            >
              {t('checkEdit.goToPayment')} ›
            </button>
          )}
        </aside>
      </div>

      {voidTarget && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setVoidTarget(null)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '90%', maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t('checkEdit.voidReasonTitle')}
            </div>
            <div style={{ padding: '16px 24px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{voidTarget.itemNameAr}</div>
              <textarea
                value={voidReason}
                onChange={(e) => setVoidReason(e.target.value)}
                rows={3}
                style={{ width: '100%', borderRadius: 'var(--radius)', border: '1px solid var(--color-border)', padding: 8, fontFamily: 'inherit', fontSize: 13 }}
                placeholder={t('checkEdit.voidReasonPlaceholder')}
              />
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setVoidTarget(null)}>{t('common.cancel')}</Button>
              <Button variant="danger" disabled={!voidReason.trim()} onClick={confirmVoid}>{t('common.remove')}</Button>
            </div>
          </div>
        </div>
      )}

      {showScanModal && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setShowScanModal(false)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '90%', maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t('checkEdit.scanQr')}
            </div>
            <div style={{ padding: '16px 24px', display: 'flex', flexDirection: 'column', gap: 8 }}>
              <div style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{t('checkEdit.scanQrHint')}</div>
              <Input
                value={scanKey}
                onChange={(e) => setScanKey(e.target.value)}
                placeholder={t('checkEdit.scanQrPlaceholder')}
              />
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setShowScanModal(false)}>{t('common.cancel')}</Button>
              <Button variant="primary" disabled={!scanKey.trim()} onClick={confirmScan}>{t('checkEdit.scanQrConfirm')}</Button>
            </div>
          </div>
        </div>
      )}

      {showDiscountModal && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setShowDiscountModal(false)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '90%', maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t('checkEdit.manualDiscount')}
            </div>
            <div style={{ padding: '16px 24px', display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  onClick={() => setDiscountType('Percentage')}
                  style={{
                    flex: 1, fontWeight: 700, fontSize: 13, padding: '9px 8px', borderRadius: 8,
                    border: `1px solid ${discountType === 'Percentage' ? 'var(--color-navy-900)' : 'var(--color-border)'}`,
                    background: discountType === 'Percentage' ? 'var(--color-navy-900)' : 'var(--color-surface-2)',
                    color: discountType === 'Percentage' ? '#fff' : 'var(--color-text-muted)', cursor: 'pointer'
                  }}
                >
                  {t('checkEdit.discountPercentage')}
                </button>
                <button
                  onClick={() => setDiscountType('Fixed')}
                  style={{
                    flex: 1, fontWeight: 700, fontSize: 13, padding: '9px 8px', borderRadius: 8,
                    border: `1px solid ${discountType === 'Fixed' ? 'var(--color-navy-900)' : 'var(--color-border)'}`,
                    background: discountType === 'Fixed' ? 'var(--color-navy-900)' : 'var(--color-surface-2)',
                    color: discountType === 'Fixed' ? '#fff' : 'var(--color-text-muted)', cursor: 'pointer'
                  }}
                >
                  {t('checkEdit.discountFixed')}
                </button>
              </div>
              <Input
                type="number" step="0.01" min={0}
                value={discountValue}
                onChange={(e) => setDiscountValue(e.target.value)}
                placeholder={discountType === 'Percentage' ? t('checkEdit.discountValuePercentagePlaceholder') : t('checkEdit.discountValueFixedPlaceholder')}
              />
              <Input
                value={discountReason}
                onChange={(e) => setDiscountReason(e.target.value)}
                placeholder={t('checkEdit.discountReasonPlaceholder')}
              />
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setShowDiscountModal(false)}>{t('common.cancel')}</Button>
              <Button variant="primary" disabled={!discountValue || Number(discountValue) <= 0 || !discountReason.trim()} onClick={confirmApplyDiscount}>
                {t('checkEdit.discountApply')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {showDrawerModal && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setShowDrawerModal(null)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '90%', maxWidth: 420 }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t(`drawerMovements.type${showDrawerModal}`)}
            </div>
            <div style={{ padding: '16px 24px', display: 'flex', flexDirection: 'column', gap: 10 }}>
              <Input
                type="number" step="0.01" min={0}
                value={drawerAmount}
                onChange={(e) => setDrawerAmount(e.target.value)}
                placeholder={t('drawerMovements.amount')}
              />
              <Input
                value={drawerReason}
                onChange={(e) => setDrawerReason(e.target.value)}
                placeholder={t('drawerMovements.reason')}
              />
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setShowDrawerModal(null)}>{t('common.cancel')}</Button>
              <Button variant="primary" disabled={!drawerAmount || Number(drawerAmount) <= 0} onClick={confirmDrawerMovement}>
                {t('drawerMovements.addMovement')}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
